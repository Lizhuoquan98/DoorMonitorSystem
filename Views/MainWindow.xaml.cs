using ControlLibrary.Models;
using DoorMonitorSystem.Assets.Database; 
using DoorMonitorSystem.Assets.Navigation;
using DoorMonitorSystem.ViewModels;
using System;
using System.Windows;  

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DoorMonitorSystem.Assets.Services.DeviceCommunicationService _commService;

        public MainWindow()
        {
            _ = new LoadDefaultData();

            // 1. 初始化通信服务实例
            _commService = new DoorMonitorSystem.Assets.Services.DeviceCommunicationService();

            // 2. 注册 Loaded 事件，确保 UI 和 DataContext (MainViewModel) 完全准备好后再启动通讯
            this.Loaded += MainWindow_Loaded;

            InitializeComponent();

            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            // 创建 MainViewModel 实例并传递 Dispatcher
           // INavigationService navigationService = new NavigationService(); 

            
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 异步启动通讯服务
            _ = _commService.StartAsync();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _commService?.Dispose();
        }
    }
}



