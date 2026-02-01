using System;
using System.Collections.Generic;
using System.Linq;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group; // Added for PanelEntity

namespace DoorMonitorSystem.Assets.Helper
{
    /// <summary>
    /// 点表自动生成工具
    /// 核心逻辑：基于“门模板”/“面板模板”和“设备列表”，自动计算并生成底层的 PLC 通信点位
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
            string protocolType = "S7")
        {
            var result = new List<DevicePointConfigEntity>();
            var filteredDoors = (doors != null) ? doors.OrderBy(d => d.SortOrder).ToList() : new List<DoorEntity>();
            
            for (int i = 0; i < filteredDoors.Count; i++)
            {
                var door = filteredDoors[i];
                // 优先使用 Door 模型中配置的起始地址，如果为0则回退到批量步长逻辑
                int doorBaseByteOffset = (door.ByteStartAddr > 0 || door.ByteLength > 0) ? door.ByteStartAddr : (i * doorStride);

                foreach (var temp in templates)
                {
                    if (temp.DoorTypeId != door.DoorTypeId) continue;

                    var point = CreatePoint(sourceDeviceId, TargetType.Door, door.Id, temp.Id,
                        $"{door.DoorName}-{temp.Description}", $"{door.DoorName}_{temp.Description}",
                        startAddress, doorBaseByteOffset, temp.ByteOffset, temp.BitIndex, protocolType, temp.LogTypeId);
                    point.Category = door.DoorName; // 填充 Category 为门名称

                    result.Add(point);
                }
            }
            return result;
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
            string protocolType = "S7")
        {
            var result = new List<DevicePointConfigEntity>();
            
            var filteredPanels = (panels != null) ? panels.OrderBy(p => p.SortOrder).ToList() : new List<PanelEntity>();

            for (int i = 0; i < filteredPanels.Count; i++)
            {
                var panel = filteredPanels[i];
                // 优先使用 Panel 模型中配置的起始地址
                int baseByteOffset = (panel.ByteStartAddr > 0 || panel.ByteLength > 0) ? panel.ByteStartAddr : (i * panelStride);

                foreach (var temp in templates)
                {
                    if (temp.PanelTypeId != panel.PanelTypeId) continue;

                    var point = CreatePoint(sourceDeviceId, TargetType.Panel, panel.Id, temp.Id,
                        $"{panel.PanelName}-{temp.Description}", $"{panel.PanelName}_{temp.Description}",
                        startAddress, baseByteOffset, temp.ByteOffset, temp.BitIndex, protocolType, temp.LogTypeId);
                    point.Category = panel.PanelName; // 填充 Category 为面板名称

                    result.Add(point);
                }
            }
            return result;
        }

        /// <summary>
        /// 通用点位创建逻辑
        /// </summary>
        private static DevicePointConfigEntity CreatePoint(
            int sourceDeviceId, TargetType targetType, int targetId, int templateId,
            string desc, string uiKey,
            int startAddr, int baseOffset, int relOffset, int bitIndex, string protocol, int logTypeId)
        {
            var point = new DevicePointConfigEntity
            {
                SourceDeviceId = sourceDeviceId,
                TargetType = targetType,
                TargetObjId = targetId,
                TargetBitConfigId = templateId,
                Description = desc,
                UiBinding = uiKey,
                BindingRole = "Read",
                IsLogEnabled = true,
                LogTypeId = logTypeId, // 使用模板预设的类型
                LogTriggerState = 2, // Default to Both Edges
                IsSyncEnabled = false
            };

            int finalByteOffset = baseOffset + relOffset;

            if (protocol.Contains("S7", StringComparison.OrdinalIgnoreCase))
            {
                // 核心修改：适配 ipconfig 内存读取模式
                // 驱动已固定 DB 号，内存已加载，此处仅配置相对于该内存块起始位置的“寄存器地址”
                // 核心修改：地址只存“字节地址”（寄存器地址）
                // 因为点位表中已有专门的 BitIndex 字段，不需要在 Address 字符串里重复包含位
                point.Address = finalByteOffset.ToString();
                point.BitIndex = bitIndex;
                point.FunctionCode = 0x84; // DB 区域
                point.DataType = "Bool";
            }
            else // Modbus
            {
                // Modbus: Address = Start + Offset (User requested 1:1 mapping)
                // Assuming 'baseOffset' (from Config) and 'relOffset' (from Template) are Register Indexes.
                // Assuming 'bitIndex' is the bit position within that register (0-15).
                
                int modbusAddr = startAddr + finalByteOffset;
                
                point.Address = modbusAddr.ToString();
                point.BitIndex = bitIndex; 
                point.DataType = "Bool";
                point.FunctionCode = 3; // Holding
            }

            return point;
        }



    }
}
