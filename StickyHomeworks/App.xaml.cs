using ClassIsland.Services;
using ElysiaFramework;
using ElysiaFramework.Interfaces;
using ElysiaFramework.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StickyHomeworks.Core.Context;
using StickyHomeworks.Services;
using StickyHomeworks.Views;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Sentry;
using System.Drawing;
using System.Windows.Forms;

namespace StickyHomeworks;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : AppEx
{
    private static Mutex? Mutex;

    private NotifyIcon _notifyIcon;

    private MainWindow MainWindow; // 假设您有一个MainWindow变量
    private ToolStripMenuItem showMainWindowItem; // 定义菜单项

    public static string AppVersion => Assembly.GetExecutingAssembly().GetName().Version!.ToString();

    public App()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        SentrySdk.Init(o =>
        {
            // Tells which project in Sentry to send events to:
            o.Dsn = "https://48c6921f7cf22181e09e16345b65fd77@o4508114075713536.ingest.us.sentry.io/4508114077417472";
            // When configuring for the first time, to see what the SDK is doing:
            o.Debug = false;
            // Set TracesSampleRate to 1.0 to capture 100% of transactions for tracing.
            // We recommend adjusting this value in production.
            o.TracesSampleRate = 1.0;
            // Set the release version of the application
            o.Release = App.AppVersion;
            // Enable auto session tracking
            o.AutoSessionTracking = true; // default: false
        });
    }

    void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Capture the exception to Sentry
        SentrySdk.CaptureException(e.Exception);

        // If you want to avoid the application from crashing:
        e.Handled = true;
    }


    protected override void OnStartup(StartupEventArgs e)
    {
        //AppContext.SetSwitch(@"Switch.System.Windows.Controls.DoNotAugmentWordBreakingUsingSpeller", true);
        Mutex = new Mutex(true, "StickyHomeworks.Lock", out var createNew);
        base.OnStartup(e);
        AppCenter.Start("51d345d3-d94e-4398-8e9a-927d22c5849a", typeof(Analytics), typeof(Crashes));
        if (!createNew)
        {
            MessageBox.Show("应用已经在运行中，请勿重复启动第二个实例。");
            Environment.Exit(0);

        }

        Host = Microsoft.Extensions.Hosting.Host.
            CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            ConfigureServices((context, services) =>
            {
                services.AddDbContext<AppDbContext>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<ProfileService>();
                services.AddSingleton<SettingsService>();
                services.AddSingleton<SettingsWindow>();
                services.AddSingleton<WallpaperPickingService>();
                services.AddHostedService<ThemeBackgroundService>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<HomeworkEditWindow>();
                services.AddSingleton<CrashWindow>();
                services.AddSingleton<WindowFocusObserverService>();
            }).
            Build();
        _ = Host.StartAsync();
        GetService<AppDbContext>();
        MainWindow = GetService<MainWindow>();
        GetService<MainWindow>().Show();
        base.OnStartup(e);

        // 创建托盘图标
        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "AppLogo.ico")),
            Text = "StickyHomeworks",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

    }

    // 创建托盘右键菜单
    private ContextMenuStrip CreateContextMenu()
            {
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("退出", null, ExitApplication);
             return contextMenu;
            }




    // 退出逻辑
    private void ExitApplication(object sender, EventArgs e)
{
    _notifyIcon.Visible = false;
    _notifyIcon.Dispose();
    Current.Shutdown();
}


private void App_OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        var cw = GetService<CrashWindow>();
        cw.CrashInfo = e.Exception.ToString();
        cw.Exception = e.Exception;
        cw.OpenWindow();
    }

    public static void ReleaseLock()
    {
        Mutex?.ReleaseMutex();
    }
}