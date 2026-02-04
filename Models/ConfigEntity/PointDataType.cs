using System.ComponentModel;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// 标准点位数据类型枚举
    /// </summary>
    public enum PointDataType
    {
        [Description("布尔 (Bool)")]
        Bool = 0,
        
        [Description("字节 (Byte)")]
        Byte = 1,
        
        [Description("有符号字节 (SByte)")]
        SByte = 2,
        
        [Description("短整型 (Int16)")]
        Int16 = 3,
        
        [Description("无符号短整型 (UInt16/Word)")]
        UInt16 = 4, // 原 Word
        
        [Description("整型 (Int32)")]
        Int32 = 5,
        
        [Description("无符号整型 (UInt32/DWord)")]
        UInt32 = 6, // 原 DWord
        
        [Description("单精度浮点 (Float)")]
        Float = 7
    }
}
