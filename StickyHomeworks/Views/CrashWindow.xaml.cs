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

    private void RestoreFromLatestBackup(string backupDirectory, string sourceFilePath) //备份json主要逻辑方法
    {
        // 获取备份目录中所有的设置文件
        string[] backupFiles = Directory.GetFiles(backupDirectory, "Settings_*.json");

        if (backupFiles.Length > 1)
        {
            // 如果备份文件大于一个，按时间排序获取前两个文件
            var sortedFiles = backupFiles.OrderByDescending(f => new FileInfo(f).LastWriteTime).Take(2).ToList();

            // 调用恢复文件的方法并传递最新两个备份文件
            foreach (var file in sortedFiles)
            {
                RestoreSettingsFile(file, sourceFilePath); //将备份文件恢复
            }
        }
        else
        {
            MessageBox.Show("没有找到备份文件。");
        }
    }

    private void RestoreSettingsFile(string sourceFilePath, string destinationFilePath) //备份json主要逻辑方法
    {
        try
        {
            // 检查目标文件是否存在，如果存在则先删除
            if (File.Exists(destinationFilePath))
            {
                File.Delete(destinationFilePath);
            }

            // 将源文件复制到目标目录并重命名
            File.Copy(sourceFilePath, destinationFilePath, true);

            // 弹出消息框，询问用户是否确认恢复备份
            MessageBoxResult result = MessageBox.Show("是否恢复备份的 Settings.json 文件？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RestartApplication();  // 恢复成功后重启应用
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