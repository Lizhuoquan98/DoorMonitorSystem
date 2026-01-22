using DoorMonitorSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// DoorInfo.xaml 的交互逻辑
    /// </summary>
    public partial class MainView : UserControl
    {

        private Point _startPoint;
        private bool _isDragging = false;

        public MainView()
        {
            InitializeComponent();

            // 订阅键盘事件（用于 ESC 键关闭弹窗）
            this.Loaded += MainView_Loaded;
            this.PreviewKeyDown += MainView_PreviewKeyDown;
        }

        private void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保控件可以接收键盘焦点
            this.Focusable = true;
            this.Focus();
        }

        /// <summary>
        /// 全局键盘事件处理（ESC 关闭弹窗）
        /// </summary>
        private void MainView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && popup.IsOpen)
            {
                var viewModel = this.DataContext as MainViewModel;
                if (viewModel != null)
                {
                    viewModel.IsPopupOpen = false;
                    popup.HorizontalOffset = 0;
                    popup.VerticalOffset = 0;
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Popup 打开时设置焦点（确保能接收键盘事件）
        /// </summary>
        private void Popup_Opened(object sender, EventArgs e)
        {
            // 给主视图设置焦点
            this.Focus();
        }

        private void Popup_KeyDown(object sender, KeyEventArgs e)
        {
            // 不再需要这个方法，已改用 MainView_PreviewKeyDown
        }

        private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;

     
             _startPoint = e.GetPosition(Window.GetWindow(this));
            var label = sender as UIElement;
            label?.CaptureMouse();
        }

        private void Label_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            var popup = this.popup; // 你的 Popup 控件名

            var currentPoint = e.GetPosition(Window.GetWindow(this));
            var offset = currentPoint - _startPoint;

            popup.HorizontalOffset += offset.X;
            popup.VerticalOffset += offset.Y;

            _startPoint = currentPoint;
        }

        private void Label_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            var label = sender as UIElement;
            label?.ReleaseMouseCapture();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            popup.HorizontalOffset  = 0;
            popup.VerticalOffset  = 0;
        }
    }
}
