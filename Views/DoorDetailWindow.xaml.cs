using System.Windows;
using System.Windows.Input;

namespace DoorMonitorSystem.Views
{
    public partial class DoorDetailWindow : Window
    {
        public DoorDetailWindow()
        {
            InitializeComponent();
            this.PreviewKeyDown += DoorDetailWindow_PreviewKeyDown;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove(); // Native OS Drag - Perfectly Smooth
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void DoorDetailWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
            }
        }
    }
}
