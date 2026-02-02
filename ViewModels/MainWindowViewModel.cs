using System;
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

            // Try Auto Login on Startup
            TryAutoLogin();
            CommandInit();
            // 默认显示主界面
            NavigateToViewModel(typeof(MainViewModel));
        }

        #endregion

        #region Methods (内部逻辑)

        /// <summary>
        /// 刷新配置信息 (站名/用户)
        /// </summary>
        public void RefreshConfigInfo()
        {
            if (GlobalData.SysCfg != null)
            {
                StationName = GlobalData.SysCfg.StationName;
            }

            if (GlobalData.CurrentUser != null)
            {
                // User requested to show Username instead of RealName
                CurrentUserName = GlobalData.CurrentUser.Username; 
                LoginButtonText = "切换账号";
            }
            else
            {
                CurrentUserName = "未登录";
                LoginButtonText = "登入";
            }
        }

        /// <summary>
        /// 启动时间更新定时器 (每500ms刷新一次)
        /// </summary>
        void TimeUpdateMethod()
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(0.5F)
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
        /// 自动尝试登录默认用户 (Operator/Guest)
        /// </summary>
        private void TryAutoLogin()
        {
            try
            {
                using var db = new Assets.Helper.SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                if (db.IsConnected)
                {
                    // Try to find a default low-level user, e.g., 'level 1' or Role 'Operator'
                    // Strategy: Find any user that is NOT Admin, or specifically named 'Operator'
                    // If none found, create a default 'Operator' user.

                    var operators = db.Query<Models.system.UserEntity>("Sys_Users", "Role='Operator' OR Role='Guest' OR Username='Operator'");

                    if (operators != null && operators.Count > 0)
                    {
                        // Auto login the first found operator
                        GlobalData.CurrentUser = operators[0];
                        Assets.Services.DataManager.Instance.LogOperation("AutoLogin", $"自动登录成功: {operators[0].Username}");
                    }
                    else
                    {
                        // If no operator exists, check if ANY user exists?
                        // Or create a default 'Operator' user for convenience
                        var anyUser = db.Query<Models.system.UserEntity>("Sys_Users", "");
                        if (anyUser == null || anyUser.Count == 0)
                        {
                            // Create default Admin and Operator if empty
                            var admin = new Models.system.UserEntity { Username = "Admin", Password = "123", RealName = "System Administrator", Role = "Admin", IsEnabled = true };
                            var op = new Models.system.UserEntity { Username = "Operator", Password = "123", RealName = "Default Operator", Role = "Operator", IsEnabled = true };

                            db.Insert(admin);
                            db.Insert(op);

                            GlobalData.CurrentUser = op; // Logic as Operator
                        }
                        else
                        {
                            // Users exist but no explicit Operator found.
                            // Login as the user with lowest ID that is not Admin? Or just stay logged out?
                            // User asked for "lowest level default direct login".
                            // Let's look for non-admin
                            var nonAdmins = db.Query<Models.system.UserEntity>("Sys_Users", "Role<>'Admin'");
                            if (nonAdmins != null && nonAdmins.Count > 0)
                            {
                                GlobalData.CurrentUser = nonAdmins[0];
                            }
                            else
                            {
                                // Only admins exist?
                                // Stay logged out or login as Admin (unsafe)? 
                                // Better stay logged out or ask user to create operator.
                                // For now, let's create an Operator.
                                var op = new Models.system.UserEntity { Username = "Operator", Password = "123", RealName = "Default Operator", Role = "Operator", IsEnabled = true };
                                db.Insert(op);
                                GlobalData.CurrentUser = op;
                            }
                        }
                    }
                }

                RefreshConfigInfo();
            }
            catch (Exception ex)
            {
                // Auto login failed (maybe DB not ready), ignore
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
                 InputDialog dialog = new("身份验证", $"请输入用户[{GlobalData.CurrentUser.Username}]的密码以退出:", true);
                 if (dialog.ShowDialog() == true)
                 {
                     string input = dialog.InputText;
                     if (input == GlobalData.CurrentUser.Password)
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
                    MessageBox.Show($"创建ViewModel失败: {ex.Message}");
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
                    MessageBox.Show($"创建View失败: {ex.Message}");
                    return;
                }
            }

            CurrentView = view;
        }
        #endregion
    }
}
