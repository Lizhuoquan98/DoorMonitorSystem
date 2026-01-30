using System.Windows;
using DoorMonitorSystem.Models.system;

namespace DoorMonitorSystem.Views
{
    public partial class UserInfoWindow : Window
    {
        public bool IsSwitchRequested { get; private set; } = false;
        public bool IsManageRequested { get; private set; } = false;

        public UserInfoWindow(UserEntity user)
        {
            InitializeComponent();
            this.DataContext = user;

            // Simple role check for Management button visibility
            if (user.Role != "Admin")
            {
                BtnManage.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnSwitch_Click(object sender, RoutedEventArgs e)
        {
            IsSwitchRequested = true;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnManage_Click(object sender, RoutedEventArgs e)
        {
            IsManageRequested = true;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
