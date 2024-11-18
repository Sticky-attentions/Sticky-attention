using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Hosting;
using StickyHomeworks.Models;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace StickyHomeworks.Services;

public class SettingsService : ObservableRecipient, IHostedService
{
    private Settings _settings = new();
    private System.Timers.Timer? _saveTimer;


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await SaveSettingsAsync();
    }


    private void ScheduleSaveSettings()
    {
        _saveTimer?.Stop();
        _saveTimer = new System.Timers.Timer(500); // 延迟 500 毫秒
        _saveTimer.Elapsed += (sender, args) =>
        {
            SaveSettings();
            _saveTimer?.Dispose();
            _saveTimer = null;
        };
        _saveTimer.Start();
    }

    public SettingsService(IHostApplicationLifetime applicationLifetime)
    {
        PropertyChanged += OnPropertyChanged;
        Settings.PropertyChanged += (o, args) => OnSettingsChanged?.Invoke(o, args);
        LoadSettingsSafeAsync();
        //applicationLifetime.ApplicationStopping.Register(SaveSettings);
        OnSettingsChanged += OnOnSettingsChanged;
    }

    private void OnOnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        ScheduleSaveSettings();
    }

    public async Task LoadSettingsSafeAsync()
    {
        try
        {
            string settingsPath = "./Settings.json";
            if (!File.Exists(settingsPath))
            {
                // 如果文件不存在，可以记录日志或者抛出一个自定义异常
                return;
            }

            string json = await File.ReadAllTextAsync(settingsPath);
            Settings settings = JsonSerializer.Deserialize<Settings>(json);

            if (settings != null)
            {
                lock (_lockObject)
                {
                    Settings = settings;
                }
            }
            else
            {
                // 如果反序列化失败，可以记录日志或者抛出一个自定义异常
            }
        }
        catch (Exception ex)
        {
            // 记录异常信息，可以是日志或者错误报告
            // 根据异常类型决定是否需要进一步处理或抛出
        }
    }

    // 在类中定义一个用于锁定的对象
    private readonly object _lockObject = new object();

    public void SaveSettings()
    {
        File.WriteAllText("./Settings.json", JsonSerializer.Serialize<Settings>(Settings));
    }

    public event PropertyChangedEventHandler? OnSettingsChanged;

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings))
        {
            Settings.PropertyChanged += (o, args) => OnSettingsChanged?.Invoke(o, args);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        SaveSettings(); // 调用同步保存方法
        return Task.CompletedTask;
    }



    public async Task SaveSettingsAsync()
    {
        var json = JsonSerializer.Serialize(Settings);
        await File.WriteAllTextAsync("./Settings.json", json);
    }

    public Settings Settings
    {
        get => _settings;
        set
        {
            if (Equals(value, _settings)) return;
            _settings = value;
            OnPropertyChanged();
        }
    }
}