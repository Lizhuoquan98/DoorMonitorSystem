using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.UControl;
using System.Collections.Generic;
using DoorMonitorSystem.Models;
using System.Collections.ObjectModel;
using Base;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 主窗口视图模型 (Shell)
    /// 负责全局导航、界面切换以及顶部/底部状态栏的逻辑。
    /// </summary>
    public class MainWindowViewModel : NotifyPropertyChanged
    {
        #region Fields (字段)

        private string _time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        private string _stationName = "XX监测站";
        private string _currentUserName = "未登录"; // Default
        private string _loginButtonText = "登入";
        private DispatcherTimer _autoLogoutTimer; // 自动登出计时器

        private object _currentViewModel;
        private UserControl _currentView;

        // 缓存 ViewModel 实例 (单例模式缓存)
        private readonly Dictionary<string, object> ViewModels = [];

        // 缓存 View 实例 (单例模式缓存)
        private readonly Dictionary<string, UserControl> Views = [];

        #endregion 

        #region Properties (属性)

        /// <summary>
        /// 实时时间显示 (用于界面顶部状态栏)
        /// </summary>
        public string Time
        {
            get { return _time; }
            set
            {
                _time = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 当前激活的视图模型 (DataTemplate 驱动)
        /// </summary>
        public object CurrentViewModel
        {
            get { return _currentViewModel; }
            set
            {
                _currentViewModel = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 导航路径树 (暂未使用)
        /// </summary>
        public ObservableCollection<PathDirectory> PathTreeList { get; set; } = [];

        /// <summary>
        /// 当前激活的视图控件 (UserControl)
        /// </summary>
        public UserControl CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public string StationName
        {
            get => _stationName;
            set { _stationName = value; OnPropertyChanged(); }
        }

        public string CurrentUserName
        {
            get => _currentUserName;
            set { _currentUserName = value; OnPropertyChanged(); }
        }

        public string LoginButtonText
        {
            get => _loginButtonText;
            set { _loginButtonText = value; OnPropertyChanged(); }
        }

        private bool _isAdmin;
        /// <summary>
        /// 是否为系统管理员权限 (Admin)
        /// 用于控制界面上高级配置入口的显示/隐藏
        /// </summary>
        public bool IsAdmin
        {
            get => _isAdmin;
            set 
            { 
                _isAdmin = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(AdminVisibility)); // Notify derived property
            }
        }

        public Visibility AdminVisibility => IsAdmin ? Visibility.Visible : Visibility.Collapsed;

        #endregion

        #region Commands (命令)

        /// <summary>
        /// 执行关闭程序命令
        /// </summary>
        public ICommand ExecuteCommand { get; private set; }

        /// <summary>
        /// 部署/保存命令 (预留)
        /// </summary>
        public ICommand DeployCommand { get; private set; }

        /// <summary>
        /// 导航切换命令
        /// 参数: ViewModel 的 Type
        /// </summary>
        public ICommand NavigationCommand { get; private set; }
        public ICommand OpenUserInfoCommand { get; private set; }
        public ICommand RefreshUICommand { get; private set; }

        #endregion

        #region Constructor (构造函数)

        public MainWindowViewModel()
        {
            TimeUpdateMethod();

            CommandInit();
            // 默认显示主界面
            NavigateToViewModel(typeof(MainViewModel));

            // 异步执行自动登录，不阻塞 UI 线程（避免 DB 连接超时导致窗口卡死）
            _ = TryAutoLoginAsync();
        }

        #endregion

        #region Methods (内部逻辑)

        /// <summary>
        /// 刷新配置信息 (站名/用户)
        /// 并处理自动登出逻辑
        /// </summary>
        public void RefreshConfigInfo()
        {
            if (GlobalData.SysCfg != null)
            {
                StationName = GlobalData.SysCfg.StationName;
            }

            // 初始化自动登出计时器
            if (_autoLogoutTimer == null)
            {
                _autoLogoutTimer = new DispatcherTimer();
                _autoLogoutTimer.Tick += (s, e) => 
                {
                    _autoLogoutTimer.Stop();
                    // 触发降级逻辑
                    Assets.Services.DataManager.Instance.LogOperation("SessionTimeout", "用户登录会话超时(2小时)，自动降级");
                    _ = TryAutoLoginAsync(); // 重新执行自动登录(默认优先Observer)
                    MessageBox.Show("登录会话已超时 (2小时)，系统自动降级为观察员模式。", "会话超时", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }
            _autoLogoutTimer.Stop(); // 先停止，重新计时

            if (GlobalData.CurrentUser != null)
            {
                // User requested to show Username instead of RealName
                CurrentUserName = GlobalData.CurrentUser.Username; 
                LoginButtonText = "切换账号";
                
                string role = GlobalData.CurrentUser.Role ?? "";
                IsAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

                // 如果非观察员模式，启动2小时倒计时
                if (!role.Equals("Observer", StringComparison.OrdinalIgnoreCase))
                {
                    _autoLogoutTimer.Interval = TimeSpan.FromHours(2); // 2小时后自动降级
                    _autoLogoutTimer.Start();
                }
            }
            else
            {
                CurrentUserName = "未登录";
                LoginButtonText = "登入";
                IsAdmin = false;
            }

            // 广播权限变更通知给所有已缓存的 ViewModel
            foreach (var vm in ViewModels.Values)
            {
                if (vm is ParameterSettingViewModel pvm)
                {
                    pvm.UpdatePermissions();
                }
                // 如果其他 VM 也有 UpdatePermissions 方法，可以在此添加
            }
        }

        /// <summary>
        /// 启动时间更新定时器 (每500ms刷新一次)
        /// </summary>
        void TimeUpdateMethod()
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1.0)
            };
            timer.Tick += (sender, e) =>
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            };
            timer.Start();
        }

        /// <summary>
        /// 初始化绑定命令
        /// </summary>
        void CommandInit()
        {
            ExecuteCommand = new RelayCommand(ExecuteCommandCallback);
            DeployCommand = new RelayCommand(DeployCommandCallback);
            NavigationCommand = new RelayCommand(NavigationCommandCallback);
            OpenUserInfoCommand = new RelayCommand(OpenUserInfoCallback);
            RefreshUICommand = new RelayCommand(async _ => 
            {
                await Assets.Services.DataManager.Instance.LoadBusinessDataAsync();
            });


        }

        /// <summary>
        /// 异步尝试登录默认用户 (Observer 观察员)
        /// 运行在后台线程，不阻塞 UI 主线程
        /// </summary>
        private async Task TryAutoLoginAsync()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;

                // 异步在后台运行 DB 查询，避免阻塞 UI
                var (currentUser, logMsg) = await Task.Run(() =>
                {
                    using var db = new Assets.Helper.SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();
                    if (!db.IsConnected) return (null, ("", ""));

                    // 1. 尝试查找 Observer 账号
                    var observers = db.Query<Models.system.UserEntity>("Sys_Users", "Role='Observer' OR Username='Observer'");

                    if (observers != null && observers.Count > 0)
                    {
                        return (observers[0], ("AutoLogin", $"自动登录成功 (观察员): {observers[0].Username}"));
                    }

                    string hashedPassword = Assets.Helper.CryptoHelper.ComputeMD5("123");
                    var anyUser = db.Query<Models.system.UserEntity>("Sys_Users", "");

                    if (anyUser == null || anyUser.Count == 0)
                    {
                        // 初始化默认账号
                        var admin = new Models.system.UserEntity { Username = "Admin", Password = hashedPassword, RealName = "System Administrator", Role = "Admin", IsEnabled = true, CreateTime = DateTime.Now };
                        var obs = new Models.system.UserEntity { Username = "Observer", Password = hashedPassword, RealName = "Default Observer", Role = "Observer", IsEnabled = true, CreateTime = DateTime.Now };
                        db.Insert(admin);
                        db.Insert(obs);
                        return (obs, ("AutoInit", "系统初始化: 创建默认 Admin 和 Observer 账号"));
                    }
                    else
                    {
                        var obs = new Models.system.UserEntity { Username = "Observer", Password = hashedPassword, RealName = "Default Observer", Role = "Observer", IsEnabled = true, CreateTime = DateTime.Now };
                        db.Insert(obs);
                        return (obs, ("AutoCreate", "自动创建并登录 Observer 账号"));
                    }
                });

                // 切回 UI 线程设置结果
                if (currentUser != null)
                {
                    GlobalData.CurrentUser = currentUser;
                    if (!string.IsNullOrEmpty(logMsg.Item1))
                        Assets.Services.DataManager.Instance.LogOperation(logMsg.Item1, logMsg.Item2);
                }
                RefreshConfigInfo();
            }
            catch (Exception ex)
            {
                // Auto login failed (maybe DB not ready), ignore and show as guest
                System.Diagnostics.Debug.WriteLine("Auto login failed: " + ex.Message);
            }
        }

        #endregion

        #region Callbacks (回调处理)

        private void DeployCommandCallback(object obj)
        {
            // Switch User Logic using proper LoginWindow
            Views.LoginWindow loginWin = new Views.LoginWindow();
            if (loginWin.ShowDialog() == true)
            {
                if (loginWin.LoggedInUser != null)
                {
                    GlobalData.CurrentUser = loginWin.LoggedInUser;
                    RefreshConfigInfo();

                    // Optional: Navigate to Main View on fresh login
                    NavigateToViewModel(typeof(MainViewModel));
                }
            }
        }

        private void OpenUserInfoCallback(object obj)
        {
            if (GlobalData.CurrentUser == null)
            {
                // If not logged in, go straight to login
                DeployCommandCallback(null);
                return;
            }

            var win = new Views.UserInfoWindow(GlobalData.CurrentUser);
            if (win.ShowDialog() == true)
            {
                if (win.IsSwitchRequested)
                {
                    // Trigger Login Logic
                    DeployCommandCallback(null);
                }
                else if (win.IsManageRequested)
                {
                    // Navigate to User Management Window
                    Views.UserManagementWindow manageWin = new Views.UserManagementWindow();
                    manageWin.ShowDialog();
                }
            }
        }

        private void NavigationCommandCallback(object obj)
        {
            if (obj is Type viewModelType)
            {
                NavigateToViewModel(viewModelType);
            }
        }

        /// <summary>
        /// 处理关闭程序请求
        /// </summary>
        private void ExecuteCommandCallback(object obj)
        {
            // 如果当前未登录，直接允许退出? 
            // 用户要求"当前用户的密码"。
            // 假设必须登录才能退出 (或者没登录就直接退出)
            if (GlobalData.CurrentUser != null)
            {
                 // 观察员禁止退出软件
                 string role = GlobalData.CurrentUser.Role ?? "";
                 if (role.Equals("Observer", StringComparison.OrdinalIgnoreCase))
                 {
                     MessageBox.Show("当前观察员模式禁止退出监控系统。", "禁止操作", MessageBoxButton.OK, MessageBoxImage.Warning);
                     return;
                 }

                 InputDialog dialog = new("身份验证", $"请输入用户[{GlobalData.CurrentUser.Username}]的密码以退出:", true);
                 if (dialog.ShowDialog() == true)
                 {
                     string input = dialog.InputText;
                     string inputMd5 = Assets.Helper.CryptoHelper.ComputeMD5(input);
                     
                     // 验证逻辑：兼容明文和MD5
                     if (input == GlobalData.CurrentUser.Password || inputMd5 == GlobalData.CurrentUser.Password)
                     {
                         // 密码正确
                         Assets.Services.DataManager.Instance.LogOperation("SystemExit", "用户验证密码后退出系统");
                         System.Windows.Application.Current.Shutdown();
                     }
                     else
                     {
                         MessageBox.Show("密码错误，禁止退出。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                         Assets.Services.DataManager.Instance.LogOperation("ExitFailed", "退出系统时密码验证失败", "Failed");
                     }
                 }
            }
            else
            {
                 // 无用户登录时直接退出? 或者禁止?
                 // 通常无用户时(比如在登录界面)可以直接关
                 Assets.Services.DataManager.Instance.LogOperation("SystemExit", "系统直接退出(无登录用户)");
                 System.Windows.Application.Current.Shutdown();
            }
        }

        /// <summary>
        /// 核心导航逻辑：切换 ViewModel 并加载对应的 View
        /// 约定优于配置：View 类名 = ViewModel 类名将 "ViewModel" 替换为 "View"
        /// </summary>
        /// <param name="viewModelType">目标 ViewModel 类型</param>
        private void NavigateToViewModel(Type viewModelType)
        {
            string key = viewModelType.FullName;

            // 1. 获取或创建 ViewModel (缓存单例)
            if (!ViewModels.TryGetValue(key, out var vm))
            {
                try
                {
                    vm = Activator.CreateInstance(viewModelType);
                    ViewModels[key] = vm;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建 ViewModel 失败: {ex.Message}");
                    return;
                }
            }
            CurrentViewModel = vm;

            // 2. 获取或创建 View (缓存单例)
            if (!Views.TryGetValue(key, out var view))
            {
                try
                {
                    // 约定命名规则：DoorMonitorSystem.ViewModels.XXXViewModel -> DoorMonitorSystem.Views.XXXView
                    string viewTypeName = key.Replace("ViewModel", "View");
                    Type viewType = Type.GetType(viewTypeName);

                    if (viewType == null)
                    {
                        MessageBox.Show($"未找到视图类型: {viewTypeName}");
                        return;
                    }

                    view = (UserControl)Activator.CreateInstance(viewType);
                    view.DataContext = vm;
                    Views[key] = view;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建 View 失败: {ex.Message}");
                    return;
                }
            }

            CurrentView = view;
        }
        #endregion
    }
}
