using ElysiaFramework;
using MaterialDesignThemes.Wpf;
using StickyHomeworks.Models;
using StickyHomeworks.Services;
using StickyHomeworks.ViewModels;
using StickyHomeworks.Views;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DataFormats = System.Windows.DataFormats;
using System.Runtime.InteropServices;
using DragEventArgs = System.Windows.DragEventArgs;
using StickyHomeworks;
using static StickyHomeworks.App;
using System.Text.Json;
using System.Threading.Tasks;
namespace StickyHomeworks
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PropertyChangedEventHandler ViewModelOnPropertyChanged;

        public MainViewModel ViewModel { get; set; } = new MainViewModel();

        public ProfileService ProfileService { get; }
        private const int WM_SYSCOMMAND = 0x0112; 
        private const int SC_MINIMIZE = 0xF020; 
        private const int GWL_STYLE = -16;      
        private const int WS_MINIMIZEBOX = 0x00020000; 

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLong(IntPtr hwnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;   // 键盘钩子
        private const int WM_KEYDOWN = 0x0100;  // 键盘按下

        private const int WM_WINDOWPOSCHANGING = 0x0046;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOZORDER = 0x0004;


        public SettingsService SettingsService { get; }

        public event EventHandler? OnHomeworkEditorUpdated;

        //string folderName = "备份";

        // 获取当前应用程序的执行目录
        string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;


        public MainWindow(ProfileService profileService,
                          SettingsService settingsService,
                          WindowFocusObserverService focusObserverService)
        {
            ProfileService = profileService;
            SettingsService = settingsService;
            // 注册自动化焦点变化事件处理器
            //Automation.AddAutomationFocusChangedEventHandler(OnFocusChangedHandler);
            InitializeComponent();
            // 注册焦点变化事件
            focusObserverService.FocusChanged += FocusObserverServiceOnFocusChanged;
            // 注册 ViewModel 属性变化事件
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            ViewModel.PropertyChanging += ViewModelOnPropertyChanging;
            // 设置窗口的数据上下文为当前窗口实例
            DataContext = this;
            // 注册窗口关闭事件（可能无效）
            Closing += OnApplicationExit;
            focusObserverService.FocusChanged += FocusObserverServiceOnFocusChanged;
            this.Loaded += MainWindow_Loaded;
            ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
            ViewModel.PropertyChanging += ViewModelOnPropertyChanging;
            DataContext = this;
            Application.Current.Exit += OnApplicationExits;
            LogHelper.Info($"主界面初始化完成！" +
                $"");
            //删除那一坨备份
            string folderName = "SA-AutoBackup";
            string currentDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string directoryPath = Path.Combine(currentDirectory, folderName); // 备份文件夹
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            int daysOld = 30; // 设置为30天
            DeleteOldFolders(directoryPath, daysOld);
            BackupSettingsJson();//实现某人没地方放的备份文件

            //低级键盘狗子
            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;

            this.StateChanged += MainWindow_StateChanged;




            string json = File.ReadAllText("Settings.json");
            dynamic settings = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

            // 获取设置中的窗口位置
            int expectedX = settings.WindowX;
            int expectedY = settings.WindowY;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Tick += (sender, e) =>
            {
                // 获取当前窗口的实际位置
                double actualX = this.Left;
                double actualY = this.Top;

                // 比较位置
                if (Math.Abs(actualX - expectedX) < 1 && Math.Abs(actualY - expectedY) < 1)
                {
                    LogHelper.Info("窗口位置与设置一致");
                }
                else
                {
                    LogHelper.Warning($"窗口位置不一致，设置位置: ({expectedX}, {expectedY})，实际位置: ({actualX}, {actualY})");
                }

                timer.Stop();
            };
            timer.Start();
        }


        

        //1.事件处理器来保存窗口位置 防止用户手动从任务管理器关闭软件而导致的无法保存位置（可能无效）
        private void OnApplicationExit(object sender, CancelEventArgs e)
        {
            // 保存窗口位置
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }
        //2.事件处理器来保存窗口位置 防止用户手动从任务管理器关闭软件而导致的无法保存位置（可能无效）
        private void OnApplicationExits(object sender, ExitEventArgs e)
        {
            // 保存窗口位置
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }


        private void FocusObserverServiceOnFocusChanged(object? sender, EventArgs e)
        {
            // 当抽屉未打开时直接返回
            if (!ViewModel.IsDrawerOpened)
                return;
            try
            {
                // 获取当前活动窗口的句柄
                var hWnd = NativeWindowHelper.GetForegroundWindow();
                // 获取当前窗口的进程 ID
                NativeWindowHelper.GetWindowThreadProcessId(hWnd, out var id);
                using var proc = Process.GetProcessById(id);
                // 如果当前进程不是应用进程，并且进程名不是特定列表中的名称，则退出编辑模式
                if (proc.Id != Environment.ProcessId &&
                    !new List<string>(["ctfmon", "textinputhost", "chsime"]).Contains(proc.ProcessName.ToLower()))
                {
                    Dispatcher.Invoke(() => ExitEditingMode());
                }
            }
            catch
            {
                // 捕获并忽略异常
            }
        }




        private void ViewModelOnPropertyChanging(object? sender, PropertyChangingEventArgs e)
        {
            // 如果属性名称是 SelectedHomework，准备退出编辑模式
            if (e.PropertyName == nameof(ViewModel.SelectedHomework))
            {
                ExitEditingMode(true);
            }
        }

        private void ExitEditingMode(bool hard = true)
        {
            // 如果当前处于创建模式，则退出创建模式
            if (ViewModel.IsCreatingMode)
            {
                ViewModel.IsCreatingMode = false;
                return;
            }
            // 如果 hard 参数为 true，则将 MainListView 的选中索引设置为 -1
            if (hard)
                MainListView.SelectedIndex = -1;
            // 关闭抽屉
            ViewModel.IsDrawerOpened = false;
            // 尝试关闭作业编辑窗口
            AppEx.GetService<HomeworkEditWindow>().TryClose();
            // 保存用户配置文件
            AppEx.GetService<ProfileService>().SaveProfile();
        }

        private void SetPos()
        {
            // 获取当前 DPI
            GetCurrentDpi(out var dpi, out _);
            // 根据保存的窗口位置和当前 DPI 设置窗口的位置
            Left = SettingsService.Settings.WindowX / dpi;
            Top = SettingsService.Settings.WindowY / dpi;
            Width = SettingsService.Settings.WindowWidth / dpi;
            Height = SettingsService.Settings.WindowHeight / dpi;
           
        }

        private void SavePos()
        {
            // 获取当前 DPI
            GetCurrentDpi(out var dpi, out _);
            // 根据当前窗口的位置和尺寸以及 DPI 保存设置
            SettingsService.Settings.WindowX = Left * dpi;
            SettingsService.Settings.WindowY = Top * dpi;
            if (ViewModel.IsExpanded)
            {
                SettingsService.Settings.WindowWidth = Width * dpi;
                SettingsService.Settings.WindowHeight = Height * dpi;
            }
        }
        public void DeleteOldFolders(string directoryPath, int daysOld)
        {
            ViewModel.IsWorking = true;
            // 获取当前时间
            DateTime now = DateTime.Now;

            // 获取目录信息
            DirectoryInfo dirInfo = new DirectoryInfo(directoryPath);

            // 检查目录是否存在
            if (!dirInfo.Exists)
            {
                ViewModel.SnackbarMessageQueue.Enqueue("备份文件夹不存在，清理操作已终止！");
                return;
            }

            // 获取所有子目录
            FileInfo[] files = dirInfo.GetFiles();
            Directory.CreateDirectory(directoryPath); // 确保目录存在
            DirectoryInfo[] subDirs = dirInfo.GetDirectories();

            foreach (DirectoryInfo subDir in subDirs)
            {
                // 检查文件夹的最后修改时间
                if ((now - subDir.LastWriteTime).TotalDays > daysOld)
                {
                    try
                    {
                        // 删除文件夹及其内容
                        subDir.Delete(true);
                        ViewModel.SnackbarMessageQueue.Enqueue($"删除备份: {subDir.FullName}");
                    }
                    catch (Exception ex)
                    {
                        // 处理可能的异常，例如权限问题
                        ViewModel.SnackbarMessageQueue.Enqueue($"无法备份文件夹 {subDir.FullName}. 原因: {ex.Message}");
                    }
                }
            }
            ViewModel.IsWorking = false;
        }
        protected void OnInitialized(EventArgs e)
        {
            // 初始化时清理过期作业
            ViewModel.ExpiredHomeworks = ProfileService.CleanupOutdated();
            if (ViewModel.ExpiredHomeworks.Count > 0)
            {
                ViewModel.CanRecoverExpireHomework = true;
                // 如果有过期作业，显示提示信息，并提供恢复选项（误了）
            }
            base.OnInitialized(e);
        }

        //防止最小化开始处
        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // 如果检测到窗口被最小化，立即还原
                WindowState = WindowState.Normal;
            }
        }


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 当窗口加载完成后调用 Automaticclarity
            Dispatcher.BeginInvoke(new Action(() =>
            {
                Automaticclarity();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            var hwnd = new WindowInteropHelper(this).Handle;
            var currentStyle = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, currentStyle.ToInt32() & ~WS_MINIMIZEBOX);

            // 注册全局钩子
            _hookID = SetHook(_proc);

    
         
            if (SettingsService.Settings.Lockwindow)
            {
              
                ButtonLock.Visibility = Visibility.Visible;
            }
            else
            { 
                
                ButtonLock.Visibility = Visibility.Collapsed;
            }


            
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
        }


        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND && (wParam.ToInt32() & 0xFFF0) == SC_MINIMIZE)
            {
                // 拦截最小化消息
                handled = true;
            }
            else if (msg == WM_WINDOWPOSCHANGING)
            {
                var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
                if ((pos.flags & SWP_SHOWWINDOW) == 0 && (pos.flags & SWP_NOZORDER) != 0 && WindowState == WindowState.Minimized)
                {
                    // 强制恢复窗口状态
                    WindowState = WindowState.Normal;
                    handled = true;
                    LogHelper.Info($"成功");
                }
            }
            return IntPtr.Zero;
        }

        // 设置全局钩子
        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        // 钩子回调函数
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                // 禁止 Alt+空格 和 Win+D
                if ((Keyboard.IsKeyDown(Key.LeftAlt) && vkCode == 0x20)) // Alt 或 Win 键
                {
                    return (IntPtr)1; // 阻止按键
                    LogHelper.Info($"成功");
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }



        private void Automaticclarity()
        {
            if (!SettingsService.Settings.lsclearances)
            {
                return ;

            }
            // 手动调用事件处理程序
            MenuItemBacktoworks_OnClick();

        }


        private void RecoverExpiredHomework()
        {
            // 恢复过期作业
            if (!ViewModel.CanRecoverExpireHomework)
                return;
            ViewModel.CanRecoverExpireHomework = false;
            var rm = ViewModel.ExpiredHomeworks;
            foreach (var i in rm)
            {
                ProfileService.Profile.Homeworks.Add(i);
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            // 设置窗口置底，并设置窗口的位置
            SetBottom();
            SetPos();
            // 注册编辑完成和主题更改事件
            AppEx.GetService<HomeworkEditWindow>().EditingFinished += OnEditingFinished;
            AppEx.GetService<HomeworkEditWindow>().SubjectChanged += OnSubjectChanged;
            base.OnContentRendered(e);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            hwndSource.AddHook(WndProc);
        }

        //防止最小化结束

        private void OnSubjectChanged(object? sender, EventArgs e)
        {
            // 当作业主题更改时，更新作业列表
            if (ViewModel.IsUpdatingHomeworkSubject)
                return;
            if (ViewModel.SelectedHomework == null)
                return;
            if (!ViewModel.IsDrawerOpened)
                return;
            ViewModel.IsUpdatingHomeworkSubject = true;
            var s = ViewModel.SelectedHomework;
            ProfileService.Profile.Homeworks.Remove(s);
            ProfileService.Profile.Homeworks.Add(s);
            ViewModel.SelectedHomework = s;
            ViewModel.IsUpdatingHomeworkSubject = false;
        }

        private void OnEditingFinished(object? sender, EventArgs e)
        {
            // 编辑完成时退出编辑模式
            ExitEditingMode();
            AutoExport();
        }

        private void ButtonCreateHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击创建作业按钮时，调用创建作业的方法
            CreateHomework();
        }

        private void CreateHomework()
        {
            // 开始创建作业
            ViewModel.IsUpdatingHomeworkSubject = true;
            OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
            var lastSubject = ViewModel.EditingHomework.Subject;
            ViewModel.IsCreatingMode = true;
            ViewModel.IsDrawerOpened = true;
            var o = new Homework()
            {
                Subject = lastSubject
            };
            ViewModel.EditingHomework = o;
            ViewModel.SelectedHomework = o;
            ProfileService.Profile.Homeworks.Add(o);
            SettingsService.SaveSettings();
            ProfileService.SaveProfile();
            ViewModel.IsUpdatingHomeworkSubject = false;
            RepositionEditingWindow();
            AppEx.GetService<HomeworkEditWindow>().TryOpen();
        }

        private void ButtonAddHomeworkCompleted_OnClick(object sender, RoutedEventArgs e)
        {
            // 完成添加作业
            ProfileService.Profile.Homeworks.Add(ViewModel.EditingHomework);
            ViewModel.IsDrawerOpened = false;
        }

        public void GetCurrentDpi(out double dpiX, out double dpiY)
        {
            // 获取当前视觉对象的 DPI 值
            var source = PresentationSource.FromVisual(this);

            dpiX = 1.0;
            dpiY = 1.0;

            if (source?.CompositionTarget != null)
            {
                dpiX = 1.0 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 1.0 * source.CompositionTarget.TransformToDevice.M22;
            }
        }

        private void ButtonSettings_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击设置按钮，打开设置窗口
            OpenSettingsWindow();
        }


        private void OpenSettingsWindow()
        {
            // 获取设置窗口服务
            var win = AppEx.GetService<SettingsWindow>();
            if (!win.IsOpened)
            {
                // 如果设置窗口未开启，则开启它
                win.IsOpened = true;
                win.Show();
            }
            else
            {
                // 如果设置窗口已开启但最小化，则恢复它
                if (win.WindowState == WindowState.Minimized)
                {
                    win.WindowState = WindowState.Normal;
                }

                // 激活设置窗口
                win.Activate();
            }
        }

        private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
        {
            // 窗口关闭事件处理
            if (!ViewModel.IsClosing)
            {
                e.Cancel = true;
                return;
            }
            //AutoExport();
            // 保存窗口位置
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }

        private void ButtonEditHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击编辑作业按钮，触发编辑事件
            OnHomeworkEditorUpdated?.Invoke(this, EventArgs.Empty);
            ViewModel.IsCreatingMode = false;

            if (ViewModel.SelectedHomework == null)
                return;

            ViewModel.EditingHomework = ViewModel.SelectedHomework;
            ViewModel.IsDrawerOpened = true;

            // 获取 HomeworkEditWindow 的实例并设置窗口位置
            var editWindow = AppEx.GetService<HomeworkEditWindow>();
            editWindow.ShowAtMousePosition(); // 在鼠标右侧打开窗口
        }



        private void ButtonRemoveHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击移除作业按钮，移除选中的作业
            ViewModel.IsUpdatingHomeworkSubject = true;
            if (ViewModel.SelectedHomework == null)
                return;
            ProfileService.Profile.Homeworks.Remove(ViewModel.SelectedHomework);
            ViewModel.IsUpdatingHomeworkSubject = false;
            AutoExport();
        }

        private void ButtonEditDone_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击编辑完成按钮，关闭抽屉
            ViewModel.IsDrawerOpened = false;
            AutoExport();
        }

        private void DragBorder_OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 当在可拖动边框上按下鼠标时，如果窗口未锁定，则开始拖动窗口
            if (ViewModel.IsUnlocked && e.LeftButton == MouseButtonState.Pressed)
            {
                SetBottom();
                DragMove();
                SetBottom();
                SavePos();//调取当前位置
                // 保存设置
                SettingsService.SaveSettings();
                // 保存用户配置文件
                ProfileService.SaveProfile();
            }
        }

        private void SetBottom()
        {
            // 如果设置中指定窗口应置于底部，则调用 SetWindowPos 方法将窗口置底
            if (!SettingsService.Settings.IsBottom)
            {
                return;
            }
            var hWnd = new WindowInteropHelper(this).Handle;
            NativeWindowHelper.SetWindowPos(hWnd, NativeWindowHelper.HWND_BOTTOM, 0, 0, 0, 0, NativeWindowHelper.SWP_NOSIZE | NativeWindowHelper.SWP_NOMOVE | NativeWindowHelper.SWP_NOACTIVATE);
        }

        private void MainWindow_OnStateChanged(object? sender, EventArgs e)
        {
            // 当窗口状态改变时，如果窗口被最大化或调整大小，调用 SetBottom 方法
            SetBottom();
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }

        private void MainWindow_OnActivated(object? sender, EventArgs e)
        {
            // 当窗口被激活时，调用 SetBottom 方法
            SetBottom();
            SavePos();
            // 保存设置
            SettingsService.SaveSettings();
            // 保存用户配置文件
            ProfileService.SaveProfile();
        }

        private void ButtonExit_OnClick(object sender, RoutedEventArgs e)
        {

            // 找到 DialogHost 控件
            var dialogHost = this.FindResource("StopConfirm") as DialogHost;
            if (dialogHost != null)
            {
                dialogHost.IsOpen = true;
            }

        }

        private void ButtonDateSetToday_OnClick(object sender, RoutedEventArgs e)
        {
            // 设置编辑中的作业的截止日期为今天
            ViewModel.EditingHomework.DueTime = DateTime.Today;
        }

        private void ButtonDateSetWeekends_OnClick(object sender, RoutedEventArgs e)
        {
            // 设置编辑中的作业的截止日期为周末
            var today = DateTime.Today;
            var delta = DayOfWeek.Saturday - today.DayOfWeek + 1;
            ViewModel.EditingHomework.DueTime = today + TimeSpan.FromDays(delta);
        }

        private void ButtonExpandingSwitcher_OnClick(object sender, RoutedEventArgs e)
        {
            // 切换窗口的展开和收缩状态
            SavePos();
            ViewModel.IsExpanded = !ViewModel.IsExpanded;
            if (ViewModel.IsExpanded)
            {
                SizeToContent = SizeToContent.Manual;
                SetPos();
            }
            else
            {
                ViewModel.IsUnlocked = false;
                SizeToContent = SizeToContent.Height;
                Width = Math.Min(ActualWidth, 350);
            }
        }

        private void MainWindow_OnDeactivated(object? sender, EventArgs e)
        {
            // 当窗口失去焦点时，可以在这里添加逻辑
            //MainListView.SelectedIndex = -1;
        }

        private async void ButtonExport_OnClick(object sender, RoutedEventArgs e)
        {
            // 设置视图模型 IsWorking 属性为 true，表示当前正在处理导出操作
            ViewModel.IsWorking = true;

            // 初始化一个文件保存对话框组件
            var dialog = new System.Windows.Forms.SaveFileDialog()
            {
                // 设置对话框中显示的文件类型过滤器
                Filter = "图片 (*.png)|*.png"
            };

            // 生成一个默认的文件名，包含时间戳
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            dialog.FileName = $"Export_{timestamp}.png"; // 自动填写文件名

            // 显示文件保存对话框，并检查是否点击了“保存”按钮
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                goto done;
            }

            // 调用 ExitEditingMode 方法退出编辑模式
            ExitEditingMode();

            // 等待一个任务调度周期，确保 UI 操作完成后再进行后续操作
            await Task.Yield();

            // 获取用户选择的文件保存路径
            var file = dialog.FileName;

            // 从设置服务中获取当前的缩放比例
            var scale = SettingsService.Settings.Scale;

            // 获取 MainListView 的实际尺寸
            var listViewWidth = MainListView.ActualWidth;
            var listViewHeight = MainListView.ActualHeight;

            // 设置背景的尺寸，比 MainListView 的内容稍大一些
            var backgroundWidth = listViewWidth * scale + 100; 
            var backgroundHeight = listViewHeight * scale + 100; 

            // 创建一个新的绘图视觉对象，用于绘制背景
            var backgroundVisual = new DrawingVisual();
            using (var context = backgroundVisual.RenderOpen())
            {
                // 从应用的资源中找到名为 MaterialDesignPaper 的画刷
                var bg = (System.Windows.Media.Brush)FindResource("MaterialDesignPaper");

                // 绘制背景
                context.DrawRectangle(bg, null, new Rect(0, 0, backgroundWidth, backgroundHeight));
            }

            // 创建一个新的绘图视觉对象，用于绘制 MainListView 的内容
            var contentVisual = new DrawingVisual();
            using (var context = contentVisual.RenderOpen())
            {
                // 创建一个新的视觉画刷，用于将 MainListView 的视觉内容绘制到绘图面上
                var brush = new VisualBrush(MainListView)
                {
                    Stretch = Stretch.None  // 设置画刷的拉伸模式为 None，即不拉伸
                };

                // 绘制 MainListView 的内容，居中放置在背景中
                context.DrawRectangle(brush, null, new Rect(50, 50, listViewWidth * scale, listViewHeight * scale));
            }

            // 创建一个新的绘图视觉对象，用于合并背景和内容
            var finalVisual = new DrawingVisual();
            using (var context = finalVisual.RenderOpen())
            {
                // 绘制背景
                context.DrawDrawing(backgroundVisual.Drawing);
                // 绘制内容
                context.DrawDrawing(contentVisual.Drawing);
            }

            // 创建一个目标为位图的渲染对象，用于将视觉对象转换为位图
            var bitmap = new RenderTargetBitmap((int)backgroundWidth, (int)backgroundHeight, 96d, 96d, PixelFormats.Default);
            bitmap.Render(finalVisual);

            // 创建一个 PNG 位图编码器，用于将位图编码为 PNG 格式
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            // 尝试将编码后的 PNG 数据保存到文件中
            try
            {
                // 使用 FileStream 创建文件流，用于写入文件
                using (var stream = new FileStream(file, FileMode.Create, FileAccess.Write))
                {
                    // 将编码器中的数据写入文件流
                    encoder.Save(stream);

                    // 调用 ShowExportSuccessMessage 方法显示导出成功的提示信息
                    await ShowExportSuccessMessage(file);
                }
            }
            catch (Exception ex)
            {
                // 如果在导出过程中发生异常，将异常信息添加到 SnackbarMessageQueue 中显示
                ViewModel.SnackbarMessageQueue.Enqueue($"导出失败：{ex.Message}");
            }

        done:
            // 释放文件保存对话框所占用的资源
            dialog.Dispose();

            // 设置视图模型的 IsWorking 属性为 false，表示导出操作已完成
            ViewModel.IsWorking = false;
        }



        private async void AutoExport()
        {
            // 如果窗口为 null 或者窗口的宽度或高度小于最小值，直接返回
            if (Application.Current.MainWindow == null ||
                Application.Current.MainWindow.ActualWidth < 1 ||
                Application.Current.MainWindow.ActualHeight < 1)
            {
                return;  // 如果窗口不可见（宽度或高度小于1），直接返回
            }

            ViewModel.IsWorking = true;

            // 文件夹名称
            string folderName = "SA-AutoBackup";
            // 使用 “yyyy-MM-dd” 格式作为文件夹名称，以每天创建一个新文件夹
            string cfolderName = System.DateTime.Now.ToString("yyyy-MM-dd");
            string currentDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);

            bool hasErrorOccurred = false;
            try
            {
                // 组合目录，并确保备份文件夹存在
                string folderPath = Path.Combine(currentDirectory, folderName, cfolderName);
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // 确保 MainListView 和 SettingsService.Settings 已初始化
                if (MainListView == null || SettingsService.Settings == null)
                {
                    throw new NullReferenceException("MainListView 或 SettingsService.Settings 未初始化！");
                }

                // 调用 ExitEditingMode 方法退出编辑模式
                ExitEditingMode();

                // 等待一个任务调度周期，确保 UI 操作完成后再进行后续操作
                await Task.Yield();

                // 文件基本名称
                string baseFileName = DateTime.Now.ToString("HH-mm-ss-fff");
                string fileExtension = ".png";

                string newFileName = $"{baseFileName}{fileExtension}";
                string filePath = Path.Combine(folderPath, newFileName);

                // 获取 MainListView 的实际宽度和高度
                double listViewWidth = MainListView.ActualWidth;
                double listViewHeight = MainListView.ActualHeight;
                var s = SettingsService.Settings.Scale;

                // 创建一个新的绘图视觉对象
                var visual = new DrawingVisual();

                // 打开视觉对象的渲染上下文
                using (var context = visual.RenderOpen())
                {
                    // 设置背景的尺寸，比 MainListView 的内容稍大一些
                    double backgroundWidth = listViewWidth * s + 100;
                    double backgroundHeight = listViewHeight * s + 100;

                    // 使用 FindResource 获取背景画刷（假设 MaterialDesignPaper 是一个 Brush 资源）
                    var bg = (System.Windows.Media.Brush)FindResource("MaterialDesignPaper");

                    // 在渲染上下文中绘制背景
                    var backgroundRect = new Rect(0, 0, backgroundWidth, backgroundHeight);
                    context.DrawRectangle(bg, null, backgroundRect);

                    // 设置 MainListView 的画刷
                    var brush = new VisualBrush(MainListView)
                    {
                        Stretch = Stretch.None  // 设置画刷的拉伸模式为 None，即不拉伸
                    };

                    // 计算截图内容在背景上的位置（居中）
                    double contentLeft = (backgroundWidth - listViewWidth * s) / 2;
                    double contentTop = (backgroundHeight - listViewHeight * s) / 2;
                    var contentRect = new Rect(contentLeft, contentTop, listViewWidth * s, listViewHeight * s);

                    // 在渲染上下文中绘制 MainListView 的内容
                    context.DrawRectangle(brush, null, contentRect);
                }

                // 获取有效的宽度和高度，确保最小尺寸
                int width = Math.Max(1, (int)(listViewWidth * s + 100));
                int height = Math.Max(1, (int)(listViewHeight * s + 100));

                // 创建一个目标为位图的渲染对象，用于将视觉对象转换为位图
                var bitmap = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
                bitmap.Render(visual);

                // 创建 PNG 位图编码器
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                // 尝试将编码后的 PNG 数据保存到文件中
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(stream);
                }
            }
            catch (Exception ex)
            {
                // 如果发生异常，设置 hasErrorOccurred 为 true
                hasErrorOccurred = true;
                ViewModel.IsWorking = true;
                // 显示失败信息
                ViewModel.SnackbarMessageQueue.Enqueue($"导出失败：{ex.Message}");
                LogHelper.Error($"导出失败：{ex.Message}");
            }
            finally
            {
                // 如果没有发生错误，确保 IsWorking 为 false
                if (!hasErrorOccurred)
                {
                    await Task.Delay(100);  // 延时操作，防止频繁更新
                    ViewModel.IsWorking = false;
                }
            }
        }



        public void ToggleWindowExpansion()
        {
            SavePos();
            ViewModel.IsExpanded = !ViewModel.IsExpanded; // 切换展开状态
            if (ViewModel.IsExpanded)
            {
                SizeToContent = SizeToContent.Manual;
                SetPos();
            }
            else
            {
                ViewModel.IsUnlocked = false;
                SizeToContent = SizeToContent.Height;
                Width = Math.Min(ActualWidth, 350);
            }
        }



        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Maximized)
            {
                // 如果窗口被最大化，尝试还原
                WindowState = WindowState.Normal;
            }
        }


        private async Task ShowExportSuccessMessage(string file)
        {
            // 将导出成功的提示信息添加到 SnackbarMessageQueue 中显示
            ViewModel.SnackbarMessageQueue.Enqueue($"成功地导出到：{file}", "查看", () =>
            {
                // 启动系统默认程序打开导出的文件
                Process.Start(new ProcessStartInfo()
                {
                    FileName = file,
                    UseShellExecute = true
                });
            });
        }

        private void DrawerHost_OnDrawerClosing(object? sender, DrawerClosingEventArgs e)
        {
            // 当抽屉关闭时，保存设置和用户配置文件
            SettingsService.SaveSettings();
            ProfileService.SaveProfile();
            AutoExport();
        }

        private void ButtonMore_Click(object sender, RoutedEventArgs e)
        {
            // 点击更多按钮，打开更多选项的弹出窗口
            PopupExAdvanced.IsOpen = true;
        }

        private void MainListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 当主列表视图的选择项发生变化时，可以在这里添加逻辑
            //ExitEditingMode(false);
        }

        public void OnTextBoxEnter()
        {
            // 如果在文本框中按下回车键，创建作业
            CreateHomework();
        }

        private void MenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            // 如果点击菜单项，关闭弹出窗口
            PopupExAdvanced.IsOpen = false;
        }

        private void MenuItemRecoverExpiredHomework_OnClick(object sender, RoutedEventArgs e)
        {
            // 如果点击恢复过期作业的菜单项，恢复过期作业
            RecoverExpiredHomework();
        }

        private void MenuItemBacktowork_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ExpiredHomeworks = ProfileService.CleanupOutdated();
            if (ViewModel.ExpiredHomeworks.Count > 0)
            {
                ViewModel.CanRecoverExpireHomework = true;
                // 如果有过期作业，显示提示信息，并提供恢复选项（误了）
            }
        }

        private void MenuItemBacktoworks_OnClick()
        {
            ViewModel.ExpiredHomeworks = ProfileService.CleanupOutdated();
            if (ViewModel.ExpiredHomeworks.Count > 0)
            {
                ViewModel.CanRecoverExpireHomework = true;
                // 如果有过期作业，显示提示信息，并提供恢复选项（误了）
                ViewModel.SnackbarMessageQueue.Enqueue($"清除了{ViewModel.ExpiredHomeworks.Count}条过期的作业。",
                "恢复", (o) => { RecoverExpiredHomework(); }, null, false, false, TimeSpan.FromSeconds(30));
            }
        }

        private void ButtonRestart_OnClick(object sender, RoutedEventArgs e)
        {
            // 点击重启按钮，重启应用程序
            App.ReleaseLock();
            System.Windows.Forms.Application.Restart();

        }
        private void MainWindow_OnDragOver(object sender, DragEventArgs e)
        {
            // 当拖动对象进入窗口时，可以在这里添加逻辑
            // 记录一条普通消息到 Sentry
        }

        private void MainWindow_OnDragEnter(object sender, DragEventArgs e)
        {
            // 当拖动对象进入窗口时，如果数据格式是文件，则处理
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;
            ViewModel.IsExpanded = false;
            ViewModel.IsUnlocked = false;
            SizeToContent = SizeToContent.Height;
            Width = Math.Min(ActualWidth, 350);
        }

        private async void RepositionEditingWindow()
        {

            // 重新定位编辑窗口
            if (ViewModel.SelectedListBoxItem == null)
            {
                Debug.WriteLine("SelectedListBoxItem is null, cannot reposition the editing window.");
                return;
            }

            try
            {
                // 获取当前屏幕的DPI
                GetCurrentDpi(out var dpiX, out var dpiY);

                // 将选定ListBoxItem的右上角坐标转换为屏幕坐标系下的点
                var listBoxItemPoint = ViewModel.SelectedListBoxItem.PointToScreen(new System.Windows.Point(ViewModel.SelectedListBoxItem.ActualWidth, 0));

                // 将WPF的Point转换为GDI+的Point
                var gdiPoint = new System.Drawing.Point((int)listBoxItemPoint.X, (int)listBoxItemPoint.Y);

                // 获取包含该点的屏幕
                var screen = System.Windows.Forms.Screen.FromPoint(gdiPoint);
                var workingArea = screen.WorkingArea;

                // 通过服务提供者获取HomeworkEditWindow实例
                var homeworkEditWindow = AppEx.GetService<HomeworkEditWindow>();

                // 确保窗口已经初始化
                if (homeworkEditWindow == null || !homeworkEditWindow.IsInitialized)
                {
                    Debug.WriteLine("HomeworkEditWindow is not initialized, cannot reposition the editing window.");
                    return;
                }

                // 如果窗口尚未加载完成，等待其加载
                if (!homeworkEditWindow.IsLoaded)
                {
                    await Task.Run(() =>
                    {
                        homeworkEditWindow.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Wait();
                    });
                }

                // 计算窗口的位置
                double left = listBoxItemPoint.X / dpiX;
                double top = Math.Min(listBoxItemPoint.Y, workingArea.Bottom - homeworkEditWindow.ActualHeight * dpiY) / dpiY;

                // 确保窗口完全在屏幕上
                left = Math.Max(workingArea.Left, Math.Min(left, workingArea.Right - homeworkEditWindow.ActualWidth));
                top = Math.Max(workingArea.Top, Math.Min(top, workingArea.Bottom - homeworkEditWindow.ActualHeight));

                // 设置窗口的位置
                homeworkEditWindow.Left = left;
                homeworkEditWindow.Top = top;

                Debug.WriteLine($"Repositioned HomeworkEditWindow to: Left={left}, Top={top}");
               LogHelper.Info($"编辑窗口OK");
            }
            catch (Exception e)
            {
                // 处理可能发生的异常
                Debug.WriteLine($"Error repositioning the editing window: {e.Message}");
            }

        }
        private void ButtonSTOP_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.IsClosing = true;
            Close();
        }
        private async Task BackupSettingsJson()
        {
            await Task.Delay(3000);
            if (SettingsService.Settings.Backupst)
            {

                try
                {
                    // 定义备份文件夹路径
                    string folderName = "SA-AutoBackup";
                    string settings_folderName = "Settings-Backups";
                    string currentDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                    string backupBaseDirectory = Path.Combine(currentDirectory, folderName, settings_folderName);

                    // 源文件路径
                    string sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.json");

                    // 确保源文件存在
                    if (!File.Exists(sourceFilePath))
                    {
                        LogHelper.Warning("Settings.json 文件不存在");
                        MessageBox.Show("Settings.json 文件不存在");
                        return;
                    }

                    // 生成基于时间戳的文件夹名称
                    string timestampFolderName = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string backupDirectory = Path.Combine(backupBaseDirectory, timestampFolderName);

                    // 如果备份文件夹不存在，则创建
                    if (!Directory.Exists(backupDirectory))
                    {
                        Directory.CreateDirectory(backupDirectory);
                    }

                    // 定义备份文件路径（保持文件名不变）
                    string backupFilePath = Path.Combine(backupDirectory, "Settings.json");

                    // 复制文件到备份文件夹
                    File.Copy(sourceFilePath, backupFilePath, true);

                    // 验证备份文件的完整性
                    if (!ValidateBackupFile(backupFilePath))
                    {
                        LogHelper.Error("setting文件已损坏。");
                        MessageBox.Show("setting文件已损坏。");
                        return;
                    }

                    LogHelper.Info($"Settings.json 已成功备份到: {backupFilePath}");
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"无法备份 Settings.json: {ex.Message}");
                    MessageBox.Show($"无法备份 Settings.json: {ex.Message}");
                }
            }
            else
            {
                LogHelper.Warning("Backupst为false，备份被终止!");
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

                // 检查文件内容是否有效
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




        private void ButtonLock_Click(object sender, RoutedEventArgs e)
        {
            // 判断遮罩层是否已经显示
            if (OverlayGrid.Visibility == Visibility.Visible)
            {
                // 遮罩层已显示，隐藏遮罩层并切换按钮图标为 "lock"
                OverlayGrid.Visibility = Visibility.Collapsed;
                ButtonLock.Content = new PackIcon { Kind = PackIconKind.Lock, Width = 18 };
                ButtonLock.ToolTip = "锁定页面";

               
                LogHelper.Info($"遮罩已隐藏");
                LogHelper.Info($"窗口位置: 左={Left}, 顶={Top}");
            }
            else
            {
           
                OverlayGrid.Visibility = Visibility.Visible;
                ButtonLock.Content = new PackIcon { Kind = PackIconKind.LockOff, Width = 18 };
                ButtonLock.ToolTip = "解锁页面";


                LogHelper.Info($"遮罩已显示");
                LogHelper.Info($"窗口位置: 左={Left}, 顶={Top}");
            }
        }
    }
}
