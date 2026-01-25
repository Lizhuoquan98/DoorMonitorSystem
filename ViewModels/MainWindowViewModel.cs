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

        #endregion 
      
        #region Constructor (构造函数)

        public MainWindowViewModel()
        {
            TimeUpdateMethod();
            CommandInit();
            // 默认显示主界面
            NavigateToViewModel(typeof(MainViewModel));
        }

        #endregion

        #region Methods (内部逻辑)

        /// <summary>
        /// 启动时间更新定时器 (每500ms刷新一次)
        /// </summary>
        void TimeUpdateMethod()
        {
            var timer = new DispatcherTimer {
                Interval = TimeSpan.FromSeconds(0.5F)  };
            timer.Tick += (sender, e) => {
                Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");  };
            timer.Start();
        }

        /// <summary>
        /// 初始化绑定命令
        /// </summary>
        void CommandInit() {
            ExecuteCommand = new RelayCommand(ExecuteCommandCallback);
            DeployCommand = new RelayCommand(DeployCommandCallback);
            NavigationCommand = new RelayCommand(NavigationCommandCallback);
        } 

        #endregion

        #region Callbacks (回调处理)
        
        private void DeployCommandCallback(object obj)
        {
            // TODO: 实现配置部署逻辑
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
            // 显示弹窗，询问用户是否要关闭程序
            var result = System.Windows.MessageBox.Show("确定要关闭程序吗？", "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                InputDialog dialog = new("输入提示", "请输当前用户密码:");
                // 显示对话框并等待用户操作
                if (dialog.ShowDialog() == true)
                {
                    // 用户点击了确定，获取输入的文本
                    _ = dialog.InputText;
                    // TODO: 验证密码逻辑                  
                }

                // 关闭程序
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
