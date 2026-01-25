using System;
using System.Windows;
using DoorMonitorSystem.ViewModels;

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// 时间同步配置窗口
    /// 这里的 Code-Behind 仅用于 ViewModel 注入和简单的窗口关闭回调绑定。
    /// </summary>
    public partial class TimeSyncWindow : Window
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="viewModel">注入的 ViewModel</param>
        public TimeSyncWindow(TimeSyncViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            
            // 绑定关闭 Action，以便 ViewModel 可以触发窗口关闭
            if (viewModel.CloseAction == null)
            {
                viewModel.CloseAction = new Action(() => this.Close());
            }
        }
    }
}
