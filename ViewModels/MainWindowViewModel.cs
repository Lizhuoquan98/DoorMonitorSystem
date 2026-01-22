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
    public class MainWindowViewModel : NotifyPropertyChanged
    {


        #region 字段
        private string _time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        private object _currentViewModel;

        private UserControl _currentView;

        // 缓存 ViewModel 实例
        private readonly Dictionary<string, object> ViewModels = [];

        // 缓存 View 实例
        private readonly Dictionary<string, UserControl> Views = [];


        #endregion 

        #region 属性

        /// <summary>
        /// 实时时间显示
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
        /// 界面切换
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
        public ObservableCollection<PathDirectory> PathTreeList { get; set; } = [];

        public UserControl CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }


        #endregion 

        #region 命令
        public ICommand ExecuteCommand { get; private set; }
        public ICommand DeployCommand { get; private set; } 
        public ICommand NavigationCommand { get; private set; }

        #endregion 
      
        #region 方法 

        /// <summary>
        /// 界面实时时间更新方法
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
        /// 命令初始化
        /// </summary>
        void CommandInit() {
            ExecuteCommand = new RelayCommand(ExecuteCommandCallback);
            DeployCommand = new RelayCommand(DeployCommandCallback);
            NavigationCommand = new RelayCommand(NavigationCommandCallback);
        } 

        #endregion

        #region 回调
        private void DeployCommandCallback(object obj)
        {

        }
        private void NavigationCommandCallback(object obj)
        {
            if (obj is Type viewModelType)
            {
                NavigateToViewModel(viewModelType);
            }
        }
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
                    // 显示用户输入的文本                    
                }

                // 关闭程序
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void NavigateToViewModel(Type viewModelType)
        {
            string key = viewModelType.FullName;

            // 获取或创建 ViewModel
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

            // 获取或创建 View
            if (!Views.TryGetValue(key, out var view))
            {
                try
                {
                    //DoorMonitorSystem.Views.DevvarlistView
                    // 约定命名：ViewModel 替换为 View
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

        public MainWindowViewModel()
        {
            TimeUpdateMethod();
            CommandInit();
            // 默认显示主界面
            NavigateToViewModel(typeof(MainViewModel));
        }

    }
}




#region  
/*
 public class MainWindowViewModel : NotifyPropertyChanged
{
    private readonly INavigationService _navigationService;

    private string _time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string Time
    {
        get => _time;
        set { _time = value; OnPropertyChanged(); }
    }

    public ObservableCollection<PathDirectory> PathTreeList { get; set; } = [];

    public object? CurrentViewModel => _navigationService.CurrentViewModel;
    public UserControl? CurrentView => _navigationService.CurrentView;

    public ICommand ExecuteCommand { get; private set; }
    public ICommand DeployCommand { get; private set; }
    public ICommand NavigationCommand { get; private set; }

    public MainWindowViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;

        TimeUpdateMethod();
        CommandInit();

        // 默认导航
        _navigationService.NavigateTo<MainViewModel>();
    }

    private void TimeUpdateMethod()
    {
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(0.5)
        };
        timer.Tick += (s, e) =>
        {
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        };
        timer.Start();
    }

    private void CommandInit()
    {
        ExecuteCommand = new RelayCommand(ExecuteCommandCallback);
        DeployCommand = new RelayCommand(DeployCommandCallback);
        NavigationCommand = new RelayCommand(NavigationCommandCallback);
    }

    private void ExecuteCommandCallback(object? obj)
    {
        var result = MessageBox.Show("确定要关闭程序吗？", "确认关闭", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var dialog = new InputDialog("输入提示", "请输当前用户密码:");
            if (dialog.ShowDialog() == true)
            {
                // 验证密码...
            }

            Application.Current.Shutdown();
        }
    }

    private void DeployCommandCallback(object? obj)
    {
        // 自定义逻辑
    }

    private void NavigationCommandCallback(object? obj)
    {
        if (obj is Type vmType)
        {
            var method = typeof(INavigationService).GetMethod(nameof(INavigationService.NavigateTo))!;
            method = method.MakeGenericMethod(vmType);
            method.Invoke(_navigationService, null);
        }
    }
}
 
 */
#endregion
