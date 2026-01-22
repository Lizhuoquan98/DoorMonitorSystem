
namespace DoorMonitorSystem 
{
     
    public enum NumberOptions
    {
        Bit0 = 0, 
        Bit1 = 1,
        Bit2 = 2,
        Bit3 = 3,
        Bit4 = 4, 
        Bit5 = 5,
        Bit6 = 6,
        Bit7 = 7,
        Bit8 = 8,
        Bit9 = 9,
        Bit10 = 10,
        Bit11 = 11,
        Bit12 = 12,
        Bit13 = 13,
        Bit14 = 14,
        Bit15 = 15,  
    }
     
    public enum EnumWarn
    { 
        None = 0,
        Alarm_0 = 1,
        Alarm_1 = 2,
    }
     
    public enum Enumlog
    {
        None = 0,
        LOG  = 1,
        Alarm = 2,
        Fault = 3,
    }

    /// <summary>
    /// 站台类型枚举
    /// </summary>
    public enum StationType
    {
        /// <summary>岛式站台（两侧门，上下镜像布局）</summary>
        Island = 1,

        /// <summary>侧式站台（单侧门，垂直布局）</summary>
        Side = 2,

        /// <summary>三线站台（垂直排列，一模一样的UI）</summary>
        ThreeTrack = 3
    }

    /// <summary>
    /// 门类型枚举
    /// </summary>
    public enum DoorType
    {
        /// <summary>滑动门（站台门主体）</summary>
        SlidingDoor = 1,

        /// <summary>应急门</summary>
        EmergencyDoor = 2,

        /// <summary>端门</summary>
        EndDoor = 3
    }

    /// <summary>
    /// 面板标题位置枚举
    /// </summary>
    public enum PanelTitlePosition
    {
        /// <summary>顶部</summary>
        Top = 1,

        /// <summary>底部</summary>
        Bottom = 2
    }











}
