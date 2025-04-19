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
using static StickyHomeworks.App;


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
        SettingsService.LoadSettingsSafeAsync(); 
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
        try
        {
            // 获取备份目录中所有的子文件夹
            string[] backupFolders = Directory.GetDirectories(backupDirectory);

            if (backupFolders.Length == 0)
            {
                LogHelper.Warning("没有找到备份文件夹。");
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
                LogHelper.Warning("没有找到有效的备份文件夹。");
                MessageBox.Show("没有找到有效的备份文件夹。");
                return;
            }

            // 在最新的备份文件夹中查找 Settings.json 文件
            string settingsBackupPath = Path.Combine(latestBackupFolder.FullName, "Settings.json");

            if (!File.Exists(settingsBackupPath))
            {
                LogHelper.Warning("最新的备份文件夹中没有找到 Settings.json 文件。");
                MessageBox.Show("最新的备份文件夹中没有找到 Settings.json 文件。");
                return;
            }

            // 验证备份文件的完整性
            if (!ValidateBackupFile(settingsBackupPath))
            {
                LogHelper.Error("备份文件已损坏。");
                MessageBox.Show("备份文件已损坏,请再试一次。");
                try
                {
                    File.Delete(settingsBackupPath);
                    LogHelper.Info("已删除损坏的备份文件。");

                    // 删除整个备份文件夹
                    try
                    {
                        Directory.Delete(Path.GetDirectoryName(settingsBackupPath), true);
                        LogHelper.Info("已删除包含损坏备份文件的整个文件夹。");
                        MessageBox.Show($"已删除包含损坏备份文件的整个文件夹。" +
                            $"请重试数遍！");
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"删除备份文件夹时出错: {ex.Message}");
                        MessageBox.Show($"删除备份文件夹时出错: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"删除损坏的备份文件时出错: {ex.Message}");
                    MessageBox.Show($"删除损坏的备份文件时出错: {ex.Message}");
                }

                return;
            }

            // 调用恢复文件的方法
            RestoreSettingsFile(settingsBackupPath, sourceFilePath);
        }
        catch (Exception ex)
        {
            LogHelper.Error($"RestoreFromLatestBackup 失败：{ex.Message}");
            MessageBox.Show($"回档失败：{ex.Message}");
        }
    }

    private bool ValidateBackupFile(string backupFilePath)
    {
        try
        {
            // 检查文件是否存在
            if (!File.Exists(backupFilePath))
            {
                return false;
            }

            // 检查文件大小是否为0
            if (new FileInfo(backupFilePath).Length == 0)
            {
                return false;
            }

            // 检查文件内容是否有效（例如，检查 JSON 文件的格式）
            // 根据实际情况实现文件内容验证逻辑
            // 以下是一个简单的 JSON 文件验证示例
            using (StreamReader reader = new StreamReader(backupFilePath))
            {
                string content = reader.ReadToEnd();
                // 尝试解析 JSON 内容
                JsonDocument.Parse(content);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error($"验证备份文件 {backupFilePath} 失败：{ex.Message}");
            return false;
        }
    }

    private void RestoreSettingsFile(string sourceFilePath, string destinationFilePath)
    {
        try
        {
            // 弹出消息框，询问用户是否确认恢复备份
            MessageBoxResult result = MessageBox.Show("是否恢复备份的 Settings.json 文件？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 确保目标文件的目录存在
                string destinationDirectory = Path.GetDirectoryName(destinationFilePath);
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                // 复制文件到目标位置，覆盖现有文件
                File.Copy(sourceFilePath, destinationFilePath, true);

                LogHelper.Info("设置文件已成功恢复。");
                MessageBox.Show("设置文件已成功恢复，请手动启动Sa。");

                // 重启应用
                RestartApplication(false);  // 恢复成功后重启应用，不保存设置
            }
            else
            {
                // 用户选择取消
                LogHelper.Info("用户取消了恢复操作。");
                MessageBox.Show("用户操作返回：NO");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"RestoreSettingsFile 失败：{ex.Message}");
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