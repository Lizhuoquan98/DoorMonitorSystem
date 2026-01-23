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

            // 启动通信服务
            _commService = new DoorMonitorSystem.Assets.Services.DeviceCommunicationService();
            // 注意：StartAsync 是异步的，这里不等待，让它在后台启动
            _ = _commService.StartAsync();

            InitializeComponent();

            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            // 创建 MainViewModel 实例并传递 Dispatcher
           // INavigationService navigationService = new NavigationService(); 

            
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            _commService?.Dispose();
        }
    }
}



