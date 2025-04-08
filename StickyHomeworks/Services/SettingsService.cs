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
        // 不保存设置，直接退出
        ExitWithoutSaving();
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
        }
        catch (Exception ex)
        {
            // 记录异常信息
        }
    }

    private readonly object _lockObject = new object();

    public void SaveSettings()
    {
        var filePath = "./Settings.json";
        var settings = Settings;

        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
            }
        }
        catch (IOException)
        {
            Thread.Sleep(1000);
        }

        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings));
        }
        catch (IOException ex)
        {
            Console.WriteLine("Error writing to file: " + ex.Message);
        }
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
        SaveSettings();
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

    // 新增方法：直接退出而不保存
    public void ExitWithoutSaving()
    {
        // 直接退出程序，不保存任何内容
        Environment.Exit(0);
    }
}