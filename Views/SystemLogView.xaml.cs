using System.Windows.Controls;

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// SystemLogView.xaml 的交互逻辑
    /// </summary>
    public partial class SystemLogView : UserControl
    {
        public SystemLogView()
        {
            InitializeComponent();
        }

        private void ClearKeyword_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.DataContext is ViewModels.SystemLogViewModel viewModel)
            {
                viewModel.Keyword = "";
            }
        }
    }
}
