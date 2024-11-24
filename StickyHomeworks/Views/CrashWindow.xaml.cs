using ElysiaFramework.Controls;
using System.ComponentModel;
using System.IO;
using System.Windows;
using StickyHomeworks.Models;
using Microsoft.Win32;
using System.Text.Json;
using StickyHomeworks.Services;


namespace StickyHomeworks.Views;

/// <summary>
/// CrashWindow.xaml 的交互逻辑
/// </summary>
public partial class CrashWindow : MyWindow
{

    private readonly SettingsService _settingsService;


    public string? CrashInfo
    {
        get;
        set;
    } = "";

    public Exception Exception
    {
        get;
        set;
    } = new();

    public CrashWindow(SettingsService settingsService)
    {
        if (settingsService == null)
        {
            throw new ArgumentNullException(nameof(settingsService), "SettingsService 为 null.");
        }

        InitializeComponent();
        DataContext = this;
        _settingsService = settingsService;
    }

    public bool IsShowed { get; set; } = false;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        IsShowed = true;
        _settingsService.LoadSettingsSafeAsync(); // 在窗口显示时加载设置
        base.OnContentRendered(e);
    }

    private void ButtonIgnore_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void ButtonRestart_OnClick(object sender, RoutedEventArgs e)
    {
        App.ReleaseLock();
        System.Windows.Forms.Application.Restart();
        Application.Current.Shutdown();
    }

    private void ButtonCopy_OnClick(object sender, RoutedEventArgs e)
    {
        TextBoxCrashInfo.Focus();
        TextBoxCrashInfo.SelectAll();
        TextBoxCrashInfo.Copy();
    }



    public void OpenWindow()
    {
        if (IsShowed)
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
        }
        else
        {
            Show();
        }
    }

    private void RestoreLatestSettingsJson()//备份json主要逻辑方法
    {
        
        string folderName = "SA-AutoBackup";
        string settings_folderName = "Settings-Backups";
        string currentDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        string backupDirectory = Path.Combine(currentDirectory, folderName, settings_folderName); // 备份文件夹

        string sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.json");

        // 检查Recover是否为true
        if (_settingsService.Settings.Recover)
        {
            RestoreFromUserSelection(backupDirectory, sourceFilePath);
        }
        else
        {
            RestoreFromLatestBackup(backupDirectory, sourceFilePath);
        }
    }

    private void RestoreFromUserSelection(string backupDirectory, string sourceFilePath)//备份json主要逻辑方法
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            Title = "选择Settings.json文件",
            InitialDirectory = backupDirectory
        };

        if (openFileDialog.ShowDialog() == true)
        {
            string selectedFilePath = openFileDialog.FileName;
            RestoreSettingsFile(selectedFilePath, sourceFilePath);
        }
    }

    private void RestoreFromLatestBackup(string backupDirectory, string sourceFilePath)//备份json主要逻辑方法
    {
        string[] backupFiles = Directory.GetFiles(backupDirectory, "Settings_*.json");
        if (backupFiles.Length > 0)
        {
            var latestBackupFile = backupFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).First();
            RestoreSettingsFile(latestBackupFile, sourceFilePath);
        }
        else
        {
            MessageBox.Show("没有找到备份文件。");
        }
    }

    private void RestoreSettingsFile(string sourceFilePath, string destinationFilePath)//备份json主要逻辑方法
    {
        try
        {
            File.Copy(sourceFilePath, destinationFilePath, true);
            MessageBoxResult result = MessageBox.Show(sourceFilePath, destinationFilePath, MessageBoxButton.YesNo, MessageBoxImage.Question);
            //MessageBoxResult result = MessageBox.Show("成功回档 点击确定进行重启", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                RestartApplication();
            }
            else
            {
                // 用户选择取消，可以在这里添加取消操作的逻辑
                // 例如，可以记录日志、关闭对话框或者返回到之前的界面等
                // 以下代码只是简单的关闭对话框，实际应用中可能需要更复杂的逻辑
                MessageBox.Show($"用户操作返回：NO");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"回档失败：{ex.Message}");
        }
    }

    private void RestartApplication()
    {
        App.ReleaseLock();
        System.Windows.Forms.Application.Restart();
        Application.Current.Shutdown();
        Close();
    }

    private void CrashWindow_OnClosed(object? sender, CancelEventArgs e)
    {
        IsShowed = false;
        Hide();
        e.Cancel = true;
    }

    private void ButtonRecover_OnClick(object sender, RoutedEventArgs e)
    {
        RestoreLatestSettingsJson();
    }
}