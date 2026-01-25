using DoorMonitorSystem.Assets.Services;
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
            if (e.Key == Key.Escape)
            {
                var viewModel = this.DataContext as MainViewModel;
                if (viewModel != null && viewModel.IsPopupOpen)
                {
                    viewModel.IsPopupOpen = false;
                }
                // 不要设置为 Handled，以免影响其他输入
                // e.Handled = true; 
            }
        }


        // --- 拖动逻辑 ---

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var header = sender as Border;
            if (header != null)
            {
                _isDragging = true;
                _startPoint = e.GetPosition(this); // 相对于整个 UserControl 的坐标
                header.CaptureMouse();
                
                // 通知 DataManager 暂停高频刷新（让路给 UI 渲染）
                DataManager.Instance.IsUIInteractionActive = true;
            }
        }

        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(this);
                double offsetX = currentPoint.X - _startPoint.X;
                double offsetY = currentPoint.Y - _startPoint.Y;

                // 获取 Transform 对象 (在 XAML 中已定义 x:Name="PopupTranslateTransform")
                var transform = this.FindName("PopupTranslateTransform") as TranslateTransform;
                if (transform != null)
                {
                    transform.X += offsetX;
                    transform.Y += offsetY;
                }

                // 更新起始点，防止累积误差
                _startPoint = currentPoint;
            }
        }

        private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                var header = sender as Border;
                header?.ReleaseMouseCapture();
                
                // 恢复数据刷新
                DataManager.Instance.IsUIInteractionActive = false;
            }
        }
    }
}
