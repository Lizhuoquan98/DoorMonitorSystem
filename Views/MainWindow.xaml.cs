using ControlLibrary.Models;
using DoorMonitorSystem.Assets.Database; 
using DoorMonitorSystem.Assets.Navigation;
using DoorMonitorSystem.ViewModels;
using System.Windows;  

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            _ = new LoadDefaultData();

            InitializeComponent();

            this.Width = SystemParameters.PrimaryScreenWidth;
            this.Height = SystemParameters.PrimaryScreenHeight;

            // 创建 MainViewModel 实例并传递 Dispatcher
           // INavigationService navigationService = new NavigationService(); 
            this.DataContext = new MainWindowViewModel( );

        }

       
    }
}



