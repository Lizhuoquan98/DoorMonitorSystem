using System;
using System.Collections.Generic;
using System.Linq;
using Communicationlib.config;
using DoorMonitorSystem.Models.ConfigEntity;
using Communicationlib.TaskEngine;
using DoorMonitorSystem;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Assets.Helper;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 通讯任务构建器
    /// 负责：
    /// 1. 点位地址归一化 (兼容 Modbus 40001, S7 DB1.100 等)
    /// 2. 智能聚合算法 (将大量分散点位自动合并为批量读取任务)
    /// 3. 设备点位范围计算
    /// </summary>
    public static class CommTaskGenerator
    {
        /// <summary>
        /// 为指定设备生成最优的轮询读取任务列表
        /// </summary>
        public static List<ProtocolTaskConfig> CreatePollingTasks(ConfigEntity device, List<DevicePointConfigEntity> points, int intervalMs)
        {
            var tasks = new List<ProtocolTaskConfig>();

            // --- 场景 A: 西门子 S7 协议 ---
            // S7 协议通常按大块读取 Byte[]，不建议进行精细点位合并。
            if (device.Protocol?.Contains("S7", StringComparison.OrdinalIgnoreCase) == true)
            {
                var paramDict = device.CommParsams?.ToDictionary(p => p.Name, p => p.Value) ?? new Dictionary<string, string>();
                
                // 获取配置参数：DB号、起始地址、字节长度
                string dbNoStr = paramDict.TryGetValue("DB号", out var db) ? db : "1";
                string startAddr = paramDict.TryGetValue("起始地址", out var sa) ? sa : "0";
                int count = paramDict.TryGetValue("字节长度", out var bl) ? (int.TryParse(bl, out var b) ? b : 100) : 100;

                // 构造 S7 地址: 如果起始地址纯数字, 组合为 DBx.y
                if (int.TryParse(startAddr, out int addrNum)) 
                {
                    startAddr = $"DB{dbNoStr}.{addrNum}";
                }
                else if (startAddr.StartsWith("DB", StringComparison.OrdinalIgnoreCase))
                {
                    // 用户已经填了完整格式 DB1.0，直接使用，忽略 DB号参数? 
                    // 或者可以做校验。这里暂按用户填写的为准
                }
                else
                {
                    // 异常回退
                    startAddr = $"DB{dbNoStr}.0";
                }

                tasks.Add(new ProtocolTaskConfig
                {
                    Type = TaskType.Read,
                    Address = startAddr,
                    Count = (ushort)count,
                    Interval = intervalMs,
                    Enabled = true
                });
                return tasks;
            }

            // --- 场景 B: Modbus 协议 (核心聚合算法) ---
            if (points == null || points.Count == 0) return tasks;

            // 获取通用参数，支持通过配置 "MaxGap" 来控制聚合粒度
            var paramsDict = device.CommParsams?.ToDictionary(p => p.Name, p => p.Value) ?? new Dictionary<string, string>();
            int maxGap = 20; // 默认智能聚合 (允许最大 20 个寄存器的空洞)
            if (paramsDict.TryGetValue("MaxGap", out var val) && int.TryParse(val, out int gap))
            {
                maxGap = gap;
            }

            LogHelper.Info($"[TaskGen] 设备 '{device.Name}' 生成任务开始. 点位数:{points.Count}, 聚合阈值(MaxGap):{maxGap}");

            // 1. 按功能码分组处理 (Modbus 03 Holding / 04 Input)
            var groups = points.GroupBy(p => AutoMapFunctionCode(p.Address));

            foreach (var group in groups)
            {
                int functionCode = group.Key;
                
                // 获取该分组下所有点位的起始地址并去重排序
                var addresses = group.Select(p => NormalizeAddress(p.Address))
                                    .Distinct()
                                    .OrderBy(a => a)
                                    .ToList();

                if (addresses.Count == 0) continue;

                int blockStart = addresses[0];
                int lastAddr = addresses[0];
                int maxBlockSize = 100; // 单包最大长度 (Modbus 标准建议不超过 125 个寄存器)

                for (int i = 1; i <= addresses.Count; i++)
                {
                    bool isLast = (i == addresses.Count);
                    int currentAddr = isLast ? -1 : addresses[i];

                    // 计算当前块的结束界限
                    int lastAddrSize = 1;
                    var pointsAtLastAddr = group.Where(p => NormalizeAddress(p.Address) == lastAddr).ToList();
                    
                    foreach (var p in pointsAtLastAddr)
                    {
                        string dt = p.DataType?.ToLower() ?? "word";
                        if (dt.Contains("float") || dt.Contains("real") || dt.Contains("dword") || dt.Contains("int32") || dt.Contains("uint32") || dt.Contains("integer"))
                        {
                            lastAddrSize = 2;
                            break;
                        }
                    }

                    int currentBlockLen = (lastAddr - blockStart + lastAddrSize);

                    // 判定是否需要闭合当前任务块
                    if (isLast || (currentAddr - lastAddr > maxGap) || (currentBlockLen > maxBlockSize))
                    {
                        var newTask = new ProtocolTaskConfig
                        {
                            Type = TaskType.Read,
                            FunctionCode = functionCode,
                            Address = blockStart.ToString(),
                            Count = (ushort)currentBlockLen,
                            Interval = intervalMs,
                            Enabled = true,
                            Description = $"AutoMerged-FC{functionCode}"
                        };
                        tasks.Add(newTask);

                        // 输出生成的任务详情 (不区分设备，统一记录)
                        LogHelper.Info($"[TaskGen] -> 生成任务: {device.Name} [FC{functionCode}] Addr:{blockStart} Len:{currentBlockLen}");

                        if (!isLast) blockStart = currentAddr;
                    }
                    lastAddr = currentAddr;
                }
            }

            return tasks;
        }

        /// <summary>
        /// 计算一个设备下所有点位覆盖的物理地址范围
        /// </summary>
        public static (ushort minAddr, ushort count) CalculateDevicePointRange(List<DevicePointConfigEntity> devicePoints)
        {
            if (devicePoints == null || devicePoints.Count == 0) return (0, 0);
            ushort min = ushort.MaxValue;
            ushort max = ushort.MinValue;
            foreach (var p in devicePoints)
            {
                int addr = NormalizeAddress(p.Address);
                if (addr >= 0)
                {
                    if (addr < min) min = (ushort)addr;
                    if (addr > max) max = (ushort)addr;
                }
            }
            if (min == ushort.MaxValue) return (0, 0);
            return (min, (ushort)(max - min + 1));
        }

        /// <summary>
        /// 自动地址类型映射 (4xxxx -> FC3, 3xxxx -> FC4)
        /// </summary>
        public static int AutoMapFunctionCode(string rawAddress)
        {
            if (rawAddress.StartsWith("4")) return 3;
         //   if (rawAddress.StartsWith("3")) return 4;
            return 3;
        }

        /// <summary>
        /// 智能地址归一化提取数字偏移量
        /// </summary>
        public static int NormalizeAddress(string rawAddress)
        {
            if (string.IsNullOrWhiteSpace(rawAddress)) return 0;
            
            // 兼容 Modbus 40001 类型地址 (减 1 得到寄存器偏移)
            if (rawAddress.Length >= 5 && (rawAddress.StartsWith("4") || rawAddress.StartsWith("3")))
            {
                if (int.TryParse(rawAddress.Substring(1), out int addr)) return Math.Max(0, addr - 1);
            }
            
            // 兼容 S7 DB1.DBX100 或 DB1.100 类型地址
            if (rawAddress.Contains("."))
            {
                var parts = rawAddress.Split('.');
                string lastPart = parts.Last();
                var digits = new string(lastPart.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int addr)) return addr;
            }
            
            if (int.TryParse(rawAddress, out int directAddr)) return directAddr;
            return 0;
        }
    }
}
