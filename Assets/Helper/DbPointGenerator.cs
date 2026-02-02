using System;
using System.Collections.Generic;
using System.Linq;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;

namespace DoorMonitorSystem.Assets.Helper
{
    /// <summary>
    /// 点表自动生成工具
    /// 核心逻辑：基于“门模板”/“面板模板”和“设备列表”，自动计算并生成底层的 PLC 通信点位。
    /// 遵循“模型配置优先”原则：如果 Door/Panel 实体已配置 ByteStartAddr，则不使用自动步长递增。
    /// </summary>
    public static class DbPointGenerator
    {
        /// <summary>
        /// 生成站台门点表配置列表
        /// </summary>
        public static List<DevicePointConfigEntity> GenerateDoorPoints(
            List<DoorEntity> doors,
            List<DoorBitConfigEntity> templates,
            int sourceDeviceId,
            int startAddress,
            int doorStride,
            string protocolType) // 移除默认值，因为现在 DataType 也是动态的
        {
            var points = new List<DevicePointConfigEntity>(); // 更改变量名为 points
            var filteredDoors = (doors != null) ? doors.OrderBy(d => d.SortOrder).ToList() : new List<DoorEntity>(); // 保留排序和过滤逻辑

            for (int i = 0; i < filteredDoors.Count; i++)
            {
                var door = filteredDoors[i];
                // 计算逻辑：若模型内 ByteStartAddr 有值（非0），则直接使用；否则按照索引*步长计算相对偏置。
                int doorBaseByteOffset = (door.ByteStartAddr > 0 || door.ByteLength > 0) ? door.ByteStartAddr : (i * doorStride);

                foreach (var temp in templates)
                {
                    if (temp.DoorTypeKeyId != door.DoorTypeKeyId) continue;

                    // 调用 CreatePoint 时传入 DataType
                    var point = CreatePoint(sourceDeviceId, TargetType.Door, door.KeyId, temp.KeyId,
                        $"{door.DoorName}-{temp.Description}", $"{door.DoorName}_{temp.Description}",
                        startAddress, doorBaseByteOffset, temp.ByteOffset, temp.BitIndex, protocolType, temp.LogTypeId,
                        temp.DataType ?? "Bool"); // 使用模板中的 DataType，如果为空则默认为 Bool
                    point.Category = door.DoorName; // 保留 Category 赋值

                    points.Add(point); // 添加到 points 列表
                }
            }
            return points; // 返回 points
        }

        /// <summary>
        /// 生成面板点表配置列表
        /// </summary>
        public static List<DevicePointConfigEntity> GeneratePanelPoints(
            List<PanelEntity> panels,
            List<PanelBitConfigEntity> templates,
            int sourceDeviceId,
            int startAddress,
            int panelStride,
            string protocolType) // 移除默认值
        {
            var points = new List<DevicePointConfigEntity>(); // 更改变量名为 points
            var filteredPanels = (panels != null) ? panels.OrderBy(p => p.SortOrder).ToList() : new List<PanelEntity>(); // 保留排序和过滤逻辑

            for (int i = 0; i < filteredPanels.Count; i++)
            {
                var panel = filteredPanels[i];
                // 同门逻辑：优先信任模型内部的起始地址配置。
                int baseByteOffset = (panel.ByteStartAddr > 0 || panel.ByteLength > 0) ? panel.ByteStartAddr : (i * panelStride);

                foreach (var temp in templates)
                {
                    if (temp.PanelKeyId != panel.KeyId) continue;

                    // 调用 CreatePoint 时传入 DataType
                    var point = CreatePoint(sourceDeviceId, TargetType.Panel, panel.KeyId, temp.KeyId,
                        $"{panel.PanelName}-{temp.Description}", $"{panel.PanelName}_{temp.Description}",
                        startAddress, baseByteOffset, temp.ByteOffset, temp.BitIndex, protocolType, temp.LogTypeId,
                        temp.DataType ?? "Bool"); // 使用模板中的 DataType，如果为空则默认为 Bool
                    point.Category = panel.PanelName; // 保留 Category 赋值

                    points.Add(point); // 添加到 points 列表
                }
            }
            return points; // 返回 points
        }

        /// <summary>
        /// 核心创建方法
        /// finalAddress = startAddr + baseOffset (模型) + relOffset (模板位)
        /// </summary>
        private static DevicePointConfigEntity CreatePoint(
            int sourceDeviceId, TargetType targetType, string targetKeyId, string templateKeyId,
            string desc, string uiKey,
            int startAddr, int baseOffset, int relOffset, int bitIndex, string protocol, int logTypeId,
            string dataType = "Bool") // 添加 dataType 参数，并设置默认值
        {
            var point = new DevicePointConfigEntity
            {
                SourceDeviceId = sourceDeviceId,
                TargetType = targetType,
                TargetKeyId = targetKeyId,
                TargetBitConfigKeyId = templateKeyId,
                Description = desc,
                UiBinding = uiKey,
                DataType = dataType, // 现在支持动态传入数据类型
                BindingRole = "Read",
                IsLogEnabled = true,
                LogTypeId = logTypeId,
                LogTriggerState = 2,
                IsSyncEnabled = false,
                State0Desc = "取消", // 默认 0 态描述
                State1Desc = "触发"  // 默认 1 态描述
            };

            int calculatedOffset = baseOffset + relOffset;

            if (protocol.Contains("S7", StringComparison.OrdinalIgnoreCase))
            {
                // S7：通常将 startAddr 作为偏置叠加（比如 DB 块内不同的对象段）
                point.Address = (startAddr + calculatedOffset).ToString();
                point.BitIndex = bitIndex;
                point.FunctionCode = 0x84; 
                // DataType 在上面已经统一赋值了
            }
            else // Modbus
            {
                // Modbus: 累加起始基地址 (startAddr) 以支持 0-based 或 1-based 切换
                point.Address = (startAddr + calculatedOffset).ToString();
                point.BitIndex = bitIndex; 
                // DataType 在上面已经统一赋值了
                point.FunctionCode = 0x03; // Modbus 读保持寄存器功能码
            }

            return point;
        }
    }
}
