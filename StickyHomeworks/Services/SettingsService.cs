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
        var filePath = "./Settings.json";
        var settings = Settings; // 假设Settings是你的设置对象

        // 尝试打开文件以检查是否有其他进程正在使用它
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
            {
                // 如果无法获取独占访问权限，则会抛出IOException
                // 在这里，我们不做任何事情，因为如果文件被成功打开，意味着没有其他进程在使用它
            }
        }
        catch (IOException)
        {
            // 如果发生IOException，意味着文件可能正在被另一个进程使用
            // 我们可以在这里添加逻辑来等待一段时间后重试
            Thread.Sleep(1000); // 等待1秒
        }

        // 现在尝试写入文件
        try
        {
            File.WriteAllText(filePath, JsonSerializer.Serialize(settings));
        }
        catch (IOException ex)
        {
            // 处理写入时的异常，例如日志记录或通知用户
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