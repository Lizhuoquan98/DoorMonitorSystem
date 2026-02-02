using System.Windows.Controls;
using DoorMonitorSystem.ViewModels;
using DoorMonitorSystem.ViewModels.ConfigItems;

namespace DoorMonitorSystem.Views
{
    public partial class LayoutConfigurationView : UserControl
    {
        public LayoutConfigurationView()
        {
            InitializeComponent();
        }

        // 简单的事件处理，当文本框内容变化时，强制更新 TreeView 显示的名称
        // 因为 TextBox 的 UpdateSourceTrigger=PropertyChanged 已经更新了 VM，
        // 我们只需调用 VM 中的 UpdateName 方法同步到 Name 属性（TreeView 绑定的是 Name）
        private void OnNameTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is ConfigItemBase item)
            {
                if (item is StationConfigItem s) s.UpdateName();
                else if (item is DoorGroupConfigItem g) g.UpdateName();
                else if (item is DoorConfigItem d) d.UpdateName();
            }
        }
    }
}
