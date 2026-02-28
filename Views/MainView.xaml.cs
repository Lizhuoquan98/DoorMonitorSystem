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
    /// MainView.xaml 的交互逻辑
    /// </summary>
    public partial class MainView : UserControl
    {

        private Point _startPoint;
        private bool _isDragging = false;
        private bool _usingDragSnapshot = false;

        public MainView()
        {
            InitializeComponent();

            // 订阅键盘事件（用于 ESC 键关闭弹窗）
            this.Loaded += MainView_Loaded;
            this.PreviewKeyDown += MainView_PreviewKeyDown;
            this.PreviewMouseLeftButtonUp += MainView_PreviewMouseLeftButtonUp;
            this.PreviewMouseMove += MainView_PreviewMouseMove;
            this.LostMouseCapture += MainView_LostMouseCapture;
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
            }
        }

        /// <summary>
        /// 点击遮罩背景关闭弹窗（符合主流 UI 交互直觉）
        /// 注意：仅当点击目标是遮罩层本身时才关闭，防止点击弹窗内容误触发
        /// </summary>
        private void OverlayBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 仅当点击源为遮罩层本身（而非弹窗内部子控件）时才关闭
            if (e.Source == OverlayContainer || e.Source == sender)
            {
                var viewModel = this.DataContext as MainViewModel;
                if (viewModel != null && viewModel.IsPopupOpen && !_isDragging)
                {
                    viewModel.ClosePopupCommand?.Execute(null);
                    e.Handled = true;
                }
            }
        }


        // --- 拖动逻辑 ---

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var header = sender as Border;
            if (header != null)
            {
                if (e.ClickCount > 1) return; // 避免双击导致抖动
                _isDragging = true;
                _startPoint = e.GetPosition(this); // 相对于整个 UserControl 的坐标
                this.CaptureMouse(); // 关键修复：在 UserControl 上捕获鼠标，防止因隐藏 Header 导致丢失捕获

                _usingDragSnapshot = TryBeginDragSnapshot();
                if (!_usingDragSnapshot)
                {
                    // 拖动时使用位图缓存，降低重绘开销
                    if (PopupBorder.CacheMode == null)
                    {
                        PopupBorder.CacheMode = new BitmapCache(1.0);
                    }
                }
                
                // 通知 DataManager 暂停高频刷新（让路给 UI 渲染）
                DataManager.Instance.IsUIInteractionActive = true;
                ControlLibrary.DoorControl.IsGlobalInteractionActive = true;
                ControlLibrary.BitControl.IsGlobalInteractionActive = true;
            }
        }

        private void Header_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateDragPosition(e.GetPosition(this));
            }
        }

        private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
        }

        private void MainView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            EndDrag();
        }

        private void MainView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                UpdateDragPosition(e.GetPosition(this));
            }
        }

        private void MainView_LostMouseCapture(object sender, MouseEventArgs e)
        {
            EndDrag();
        }

        private void EndDrag()
        {
            if (!_isDragging) return;

            _isDragging = false;
            if (this.IsMouseCaptured)
            {
                this.ReleaseMouseCapture();
            }

            if (_usingDragSnapshot)
            {
                EndDragSnapshot();
            }
            else
            {
                // 释放位图缓存，恢复正常渲染
                if (PopupBorder.CacheMode != null)
                {
                    PopupBorder.CacheMode = null;
                }
            }
            _usingDragSnapshot = false;
            
            // 恢复数据刷新
            DataManager.Instance.IsUIInteractionActive = false;
            ControlLibrary.DoorControl.IsGlobalInteractionActive = false;
            ControlLibrary.BitControl.IsGlobalInteractionActive = false;
        }

        private void UpdateDragPosition(Point currentPoint)
        {
            if (!_isDragging) return;

            double offsetX = currentPoint.X - _startPoint.X;
            double offsetY = currentPoint.Y - _startPoint.Y;

            // 获取 Transform 对象 (在 XAML 中已定义 x:Name="PopupTranslateTransform")
            if (PopupTranslateTransform != null)
            {
                PopupTranslateTransform.X += offsetX;
                PopupTranslateTransform.Y += offsetY;
            }

            // 更新起始点，防止累积误差
            _startPoint = currentPoint;
        }

        private bool TryBeginDragSnapshot()
        {
            if (PopupBorder == null || PopupDragSnapshot == null) return false;

            // 1. 强制更新布局，确保获取到最新的 RenderSize
            PopupBorder.UpdateLayout();
            Size size = PopupBorder.RenderSize;
            if (size.Width < 1 || size.Height < 1) return false;

            var dpi = VisualTreeHelper.GetDpi(this);

            // 3. 创建正确 DPI 的位图
            RenderTargetBitmap rtb = new RenderTargetBitmap(
                (int)Math.Ceiling(size.Width * dpi.DpiScaleX),
                (int)Math.Ceiling(size.Height * dpi.DpiScaleY),
                dpi.PixelsPerDip * 96,
                dpi.PixelsPerDip * 96,
                PixelFormats.Pbgra32);

            // 2. 暂时移除变换。这是解决“点击位置不同，剪切不同”的关键。
            // 因为 VisualBrush 默认会捕捉包含 RenderTransform 在内的视觉表现。
            var oldTransform = PopupBorder.RenderTransform;
            PopupBorder.RenderTransform = null;

            // 3. 使用 DrawingVisual 隔离布局位置
            // RenderTargetBitmap.Render(liveElement) 会受元素在父容器中位置的影响
            // 而通过 VisualBrush 绘制到 DrawingVisual 的 (0,0) 点则完全可控
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(PopupBorder) { Stretch = Stretch.None };
                dc.DrawRectangle(vb, null, new Rect(0, 0, size.Width, size.Height));
            }

            // 4. 渲染到快照位图
            rtb.Render(dv);

            // 5. 立即恢复原控件的变换，确保逻辑连续性
            PopupBorder.RenderTransform = oldTransform;

            PopupDragSnapshot.Source = rtb;
            PopupDragSnapshot.Width = size.Width;
            PopupDragSnapshot.Height = size.Height;

            PopupDragSnapshot.RenderTransform = PopupTranslateTransform;
            PopupDragSnapshot.Visibility = Visibility.Visible;
            PopupBorder.Visibility = Visibility.Hidden;
            return true;
        }

        private void EndDragSnapshot()
        {
            if (PopupBorder == null || PopupDragSnapshot == null) return;

            PopupBorder.Visibility = Visibility.Visible;
            PopupDragSnapshot.Visibility = Visibility.Collapsed;
            PopupDragSnapshot.Source = null;
        }
    }
}
