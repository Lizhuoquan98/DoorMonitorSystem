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

        private void Popup_KeyDown(object sender, KeyEventArgs e)
        {
            // 不再需要这个方法，已改用 MainView_PreviewKeyDown
        }


    }
}
