using DoorMonitorSystem.Assets.Services;
using System;
using System.Windows;

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// 主窗口 (入口 Shell)
    /// 负责程序的生命周期管理、通讯服务的启动与停止。
    /// </summary>
    public partial class MainWindow : Window
    {
        private DoorMonitorSystem.Assets.Services.DeviceCommunicationService _commService;

        public MainWindow()
        {
            // 使用统一的数据管理器进行基础配置初始化
            DataManager.Instance.Initialize();

            // 1. 初始化通信服务实例
            _commService = new DoorMonitorSystem.Assets.Services.DeviceCommunicationService();

            // 2. 注册 Loaded 事件，确保 UI 和 DataContext (MainViewModel) 完全准备好后再启动通讯
            this.Loaded += MainWindow_Loaded;

            InitializeComponent();

            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            // 创建 MainViewModel 实例并传递 Dispatcher (已在 MainWindowViewModel 中隐式处理或 DataContext 绑定)
            
            this.Closed += MainWindow_Closed;
        }

        /// <summary>
        /// 窗口加载完成后触发
        /// 异步启动后台通讯服务
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _ = _commService.StartAsync();
        }

        /// <summary>
        /// 窗口关闭时触发
        /// 释放通讯服务资源
        /// </summary>
        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _commService?.Dispose();
        }
    }
}



