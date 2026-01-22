using DoorMonitorSystem.Models.RunModels;
using System.Collections.ObjectModel;

namespace DoorMonitorSystem.Assets.Helper
{
    /// <summary>
    /// 分类分组辅助类（用于弹窗按分类显示点位）
    /// </summary>
    public class CategoryGroup
    {
        /// <summary>分类对象</summary>
        public BitCategoryModel Category { get; set; }

        /// <summary>该分类下的点位集合</summary>
        public ObservableCollection<DoorBitConfig> Bits { get; set; }

        /// <summary>激活的点位数量</summary>
        public int ActiveCount { get; set; }
    }
}
