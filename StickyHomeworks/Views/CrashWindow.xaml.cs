using ElysiaFramework.Controls;
using System.ComponentModel;
using System.IO;
using System.Windows;
using StickyHomeworks.Models;
using Microsoft.Win32;
using System.Text.Json;
using StickyHomeworks.Services;
using Microsoft.Extensions.Hosting.Internal;
using System.Diagnostics;


namespace StickyHomeworks.Views;

/// <summary>
/// CrashWindow.xaml 的交互逻辑
/// </summary>
public partial class CrashWindow : MyWindow
{


    public ProfileService ProfileService { get; }

    public SettingsService SettingsService { get; }


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

    public CrashWindow(ProfileService profileService,
                          SettingsService settingsService)
    {
        if (settingsService == null)
        {
            throw new ArgumentNullException(nameof(settingsService), "SettingsService 为 null.");
        }

        InitializeComponent();
        DataContext = this;
        ProfileService = profileService;
        SettingsService = settingsService;
    }

    public bool IsShowed { get; set; } = false;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        IsShowed = true;
        SettingsService.LoadSettingsSafeAsync(); // 在窗口显示时加载设置
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
        TextBoxCrashInfo.SelectionStart = 0;
        TextBoxCrashInfo.SelectionLength = 0;
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

    private void RestoreLatestSettingsJson()
    {
        string folderName = "SA-AutoBackup";
        string settings_folderName = "Settings-Backups";
        string currentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string backupDirectory = Path.Combine(currentDirectory, folderName, settings_folderName);

        string sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.json");

        // 检查备份目录是否存在
        if (!Directory.Exists(backupDirectory))
        {
            MessageBox.Show("备份目录不存在：" + backupDirectory);
            return;
        }

        // 检查恢复模式
        if (SettingsService.Settings.Recover)
        {
            RestoreFromUserSelection(backupDirectory, sourceFilePath);
        }
        else
        {
            RestoreFromLatestBackup(backupDirectory, sourceFilePath);
        }
    }

    private void RestoreFromUserSelection(string backupDirectory, string sourceFilePath)
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

    private void RestoreFromLatestBackup(string backupDirectory, string sourceFilePath)
    {
        // 获取备份目录中所有的子文件夹
        string[] backupFolders = Directory.GetDirectories(backupDirectory);

        if (backupFolders.Length == 0)
        {
            MessageBox.Show("没有找到备份文件夹。");
            return;
        }

        // 按文件夹名称（时间戳）排序，找到最新的文件夹
        var latestBackupFolder = backupFolders
            .Select(f => new DirectoryInfo(f))
            .OrderByDescending(d => d.Name)
            .FirstOrDefault();

        if (latestBackupFolder == null)
        {
            MessageBox.Show("没有找到有效的备份文件夹。");
            return;
        }

        // 在最新的备份文件夹中查找 Settings.json 文件
        string settingsBackupPath = Path.Combine(latestBackupFolder.FullName, "Settings.json");

        if (!File.Exists(settingsBackupPath))
        {
            MessageBox.Show("最新的备份文件夹中没有找到 Settings.json 文件。");
            return;
        }

        // 调用恢复文件的方法
        RestoreSettingsFile(settingsBackupPath, sourceFilePath);
    }

    private void RestoreSettingsFile(string sourceFilePath, string destinationFilePath)
    {
        try
        {
            // 弹出消息框，询问用户是否确认恢复备份
            MessageBoxResult result = MessageBox.Show("是否恢复备份的 Settings.json 文件？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 动态生成批处理文件内容
                string batchContent = $@"
                    @echo off

                    REM 获取参数
                    set sourceFile={sourceFilePath}
                    set destinationFile={destinationFilePath}

                    REM 删除目标文件（如果存在）
                    if exist ""%destinationFile%"" (
                    del ""%destinationFile%""
                    )

                    REM 复制源文件到目标位置
                    copy ""%sourceFile%"" ""%destinationFile%"" > nul

                    REM 重启应用（可选）
                    start "" ""{AppDomain.CurrentDomain.FriendlyName}""

                    REM 退出批处理
                    exit
                    ";

                // 生成临时批处理文件
                string tempBatchPath = Path.Combine(Path.GetTempPath(), "RestoreSettings.bat");
                File.WriteAllText(tempBatchPath, batchContent);

                // 启动批处理文件
                Process.Start(tempBatchPath);

                // 退出应用
                RestartApplication(false);  // 恢复成功后重启应用，不保存设置
            }
            else
            {
                // 用户选择取消
                MessageBox.Show("用户操作返回：NO");
            }
        }
        catch (Exception ex)
        {
            // 出现异常时，显示错误信息
            MessageBox.Show($"回档失败：{ex.Message}");
        }
    }
    private void RestartApplication(bool saveSettings = true)
    {
        // 直接退出程序，不保存任何内容
        Environment.Exit(0);
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