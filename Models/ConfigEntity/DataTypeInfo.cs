using System;
using System.Collections.Generic;

namespace DoorMonitorSystem.Models.ConfigEntity
{
    /// <summary>
    /// 标准点位数据类型定义
    /// </summary>
    public static class DataTypeInfo
    {
        /// <summary>
        /// 获取所有标准数据类型列表 (用于 UI 下拉框)
        /// </summary>
        public static List<string> GetStandardTypes()
        {
            return new List<string>(Enum.GetNames(typeof(PointDataType)));
        }

        /// <summary>
        /// 获取类型的字节长度
        /// </summary>
        public static int GetByteLength(string type)
        {
            switch (type?.ToUpper())
            {
                case "BOOL":
                case "BIT":
                    return 0; // 特殊处理位偏移
                case "BYTE":
                case "SBYTE":
                    return 1;
                case "INT16":
                case "UINT16":
                case "WORD":
                case "SHORT":
                    return 2;
                case "INT32":
                case "UINT32":
                case "DWORD":
                case "DINT":
                case "INTEGER":
                case "FLOAT":
                case "REAL":
                    return 4;
                default:
                    return 2;
            }
        }
    }
}
