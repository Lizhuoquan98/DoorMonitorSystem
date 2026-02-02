using System.Collections.ObjectModel;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 面板模型（单个控制面板）
    /// </summary>
    public partial class PanelModel : NotifyPropertyChanged
    {
        #region 基础属性

        /// <summary>面板ID（主键，用于UI路径定位）</summary>
        public int PanelId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        public string KeyId { get; set; }

        /// <summary>所属面板组ID（外键）</summary>
        public int PanelGroupId { get; set; }

        /// <summary>面板名称，如：TBP、PSL1、PSL2等</summary>
        private string _panelName = "";
        public string PanelName
        {
            get => _panelName;
            set
            {
                _panelName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 标题位置（顶部/底部）
        /// 用于上下行站台的面板对称显示
        /// </summary>
        public PanelTitlePosition TitlePosition { get; set; } = PanelTitlePosition.Bottom;

        /// <summary>点位布局行数（如 2×3 中的 2，0表示自动）</summary>
        public int LayoutRows { get; set; } = 0;

        /// <summary>点位布局列数（如 2×3 中的 3，1表示垂直单列）</summary>
        public int LayoutColumns { get; set; } = 1;

        /// <summary>面板排序序号</summary>
        public int SortOrder { get; set; }

        #endregion

        #region 点位配置集合

        /// <summary>面板点位列表</summary>
        public ObservableCollection<PanelBitConfig> BitList { get; set; } = new();

        #endregion
    }
}
