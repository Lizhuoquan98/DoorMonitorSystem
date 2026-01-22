using DoorMonitorSystem.Models.RunModels;
using System.Windows;
using System.Windows.Controls;

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// 站台显示项模板选择器
    /// 根据数据类型自动选择对应的 DataTemplate
    /// - DoorGroup -> DoorGroupTemplate
    /// - PanelGroup -> PanelGroupTemplate
    /// </summary>
    public class StationItemTemplateSelector : DataTemplateSelector
    {
        /// <summary>门组模板</summary>
        public DataTemplate? DoorGroupTemplate { get; set; }

        /// <summary>面板组模板</summary>
        public DataTemplate? PanelGroupTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                DoorGroup => DoorGroupTemplate,
                PanelGroup => PanelGroupTemplate,
                _ => base.SelectTemplate(item, container)
            };
        }
    }
}
