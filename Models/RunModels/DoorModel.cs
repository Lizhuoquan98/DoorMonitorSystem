using System.Collections.ObjectModel;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.Models.RunModels
{
    /// <summary>
    /// 门模型（单个站台门，UI绑定使用）
    /// 门显示分三部分：头部颜色条(带门名称) + 中间图形(质量图) + 底部颜色条(锁闭状态)
    /// 只存储最终显示结果，优先级裁决由业务逻辑层处理
    /// </summary>
    public partial class DoorModel : NotifyPropertyChanged
    {
        #region 基础属性

        /// <summary>门ID（主键，用于UI路径定位）</summary>
        public int DoorId { get; set; }

        /// <summary>全局唯一标识 (GUID)</summary>
        public string KeyId { get; set; }

        /// <summary>所属门组ID（外键，通过ID关联门组）</summary>
        public int DoorGroupId { get; set; }

        /// <summary>门名称/门号（显示在头部颜色条中）</summary>
        private string _doorName = "";
        public string DoorName
        {
            get => _doorName;
            set
            {
                _doorName = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 门类型（决定使用哪套点位配置）
        /// 滑动门、应急门、端门使用不同的详细点表
        /// </summary>
        public DoorType DoorType { get; set; } = DoorType.SlidingDoor;

        /// <summary>门类型代码（用于配置关联）</summary>
        public string DoorTypeCode { get; set; } = "";

        /// <summary>门排序序号（用于界面显示顺序）</summary>
        public int SortOrder { get; set; }

        /// <summary>弹窗点位布局行数（从门类型配置读取，0表示自动）</summary>
        public int PopupLayoutRows { get; set; } = 0;

        /// <summary>弹窗点位布局列数（从门类型配置读取，2表示2列）</summary>
        public int PopupLayoutColumns { get; set; } = 2;

        #endregion

        #region 点位配置集合

        /// <summary>
        /// 门的所有点位配置（根据门类型从模板复制）
        /// 点击门时，弹窗显示该集合中的所有点位详情
        /// </summary>
        public ObservableCollection<DoorBitConfig> Bits { get; set; } = new();

        #endregion

        #region 最终显示结果

        /// <summary>
        /// 门的视觉显示结果（由业务逻辑层根据优先级裁决后更新）
        /// 包含三部分：
        /// - Visual.HeaderBackground: 头部颜色条颜色
        /// - Visual.Icons: 中间图形集合（矢量图对象）
        /// - Visual.BottomBackground: 底部颜色条颜色
        /// </summary>
        public DoorVisualResult Visual { get; set; } = new();

        /// <summary>
        /// 视觉状态指纹：用于记录上一次裁决结果的关键特征汇聚（HeaderID + ImageID + BottomID）
        /// 如果指纹未变，则无需重新克隆图形和触发 UI 刷新，极大降低 CPU 占用。
        /// </summary>
        public string LastVisualStateFingerprint { get; set; } = "";

        #endregion

        #region 交互属性

        /// <summary>
        /// 门是否被选中（点击状态）
        /// 选中时弹出详情弹窗，显示所有点位信息
        /// </summary>
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 打开详情命令（直接绑定，避免 RelativeSource 查找开销）
        /// </summary>
        private System.Windows.Input.ICommand? _openDetailCommand;
        public System.Windows.Input.ICommand? OpenDetailCommand 
        { 
            get => _openDetailCommand;
            set { _openDetailCommand = value; OnPropertyChanged(); }
        }

        #endregion
    }
}
