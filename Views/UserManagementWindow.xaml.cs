using System.Windows;
using System.Windows.Input;

namespace DoorMonitorSystem.Views
{
    public partial class UserManagementWindow : Window
    {
        public UserManagementWindow()
        {
            InitializeComponent();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
