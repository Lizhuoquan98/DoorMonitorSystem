using System;
using System.Collections.Generic;
using System.Linq;
using Communicationlib.TaskEngine;
using Communicationlib.Core;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Assets.Services;
using DoorMonitorSystem;
using Communicationlib; // 包含 ICommBase

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 数据处理引擎 (核心处理中心)
    /// 负责：
    /// 1. 将通讯层收到的原始字节(Byte)或线圈(Ushort)切片，提取出具体位和多字节类型。
    /// 2. 触发业务更新流水线 (UI映射 -> 转发同步 -> 日志存储)。
    /// </summary>
    /// <remarks>
    /// 【15,000+ 点位场景优化说明】：
    /// 1. 算法复杂度优化：将原来的 O(N) 全量点位扫描算法升级为 O(1) 哈希索引查找。
    ///    - 旧逻辑：每收到一个数据包，都必须循环 15,000 次来判断哪些点位需要更新。
    ///    - 新逻辑：通过 RebuildAddressIndex 预建索引，每收到一个包，只遍历包内包含的地址（通常 < 100个），性能提升百倍。
    /// 2. 零冗余解析：预计算所有点位的“归一化数字地址”，避免在高频通讯循环中反复进行字符串切割和格式转换。
    /// 3. 内存稳定性：采用二级字典索引（设备->地址->点位列表），极大降低了 ProcessData 过程中的 GC（垃圾回收）压力。
    /// </remarks>
    public class DataProcessor
    {
        private readonly IDictionary<int, CommRuntime> _runtimes;
        private readonly IDictionary<int, ICommBase> _slaves;
        private readonly IDictionary<int, List<DevicePointConfigEntity>> _devicePointsCache;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DataProcessor(
            IDictionary<int, CommRuntime> runtimes, 
            IDictionary<int, ICommBase> slaves, 
            IDictionary<int, List<DevicePointConfigEntity>> devicePointsCache)
        {
            _runtimes = runtimes;
            _slaves = slaves;
            _devicePointsCache = devicePointsCache;
            
            // 启动时或配置变更时构建查找索引
            RebuildAddressIndex();
        }

        /// <summary>
        /// 高性能二级索引字典：[设备ID] -> [归一化地址] -> [此地址绑定的所有点位配置]
        /// 用于将通讯层受影响的物理地址瞬间映射到业务点位。
        /// </summary>
        private Dictionary<int, Dictionary<int, List<DevicePointConfigEntity>>> _addressIndex = new();

        /// <summary>
        /// 【关键优化：构建地址查找索引】
        /// 预处理点位地址，将 $O(N)$ 搜索降阶为 $O(1)$ 查找。
        /// </summary>
        public void RebuildAddressIndex()
        {
            var newIndex = new Dictionary<int, Dictionary<int, List<DevicePointConfigEntity>>>();
            foreach (var kvp in _devicePointsCache)
            {
                int devId = kvp.Key;
                var devPoints = kvp.Value;
                var devAddrMap = new Dictionary<int, List<DevicePointConfigEntity>>();

                foreach (var p in devPoints)
                {
                    // 将 "40001" 等地址预先解析为数字偏移量 0
                    int addr = CommTaskGenerator.NormalizeAddress(p.Address);
                    if (!devAddrMap.ContainsKey(addr)) devAddrMap[addr] = new List<DevicePointConfigEntity>();
                    devAddrMap[addr].Add(p);
                }
                newIndex[devId] = devAddrMap;
            }
            _addressIndex = newIndex;
            LogHelper.Info($"[DataProcessor] 高性能地址索引重建完成，覆盖 {newIndex.Count} 台设备，准备应对大规模数据流。");
        }

        /// <summary>
        /// 处理数据入口 (Modbus/通用通道)
        /// </summary>
        /// <param name="rawData">Modbus 为 ushort[] 数组</param>
        public void ProcessData(int sourceDeviceId, string startAddrTag, object rawData)
        {
            // 如果是字节流格式(S7)则路由转发
            if (rawData is byte[] rawBytes)
            {
                ProcessByteStreamData(sourceDeviceId, startAddrTag, rawBytes);
                return;
            }

            ushort[]? data = rawData as ushort[];
            if (data == null || data.Length == 0) return;

            // 1. 获取该设备专属的查找表
            if (!_addressIndex.TryGetValue(sourceDeviceId, out var addrMap)) return;

            // 2. 转换起始地址
            int startAddr = CommTaskGenerator.NormalizeAddress(startAddrTag);

            // 3. 【高性能循环】：仅按收到数据的长度进行遍历
            for (int i = 0; i < data.Length; i++)
            {
                int currentAddr = startAddr + i;
                
                // 仅当该物理地址有绑定的点位时才执行后续解析
                if (addrMap.TryGetValue(currentAddr, out var affectedPoints))
                {
                    foreach (var p in affectedPoints)
                    {
                        // 提取单字值
                        ushort wordValue = data[i];
                        bool bitValue = ((wordValue >> p.BitIndex) & 1) == 1;
                        object rawObjValue = wordValue; 
                        string dType = p.DataType?.ToUpper() ?? "WORD";

                        // --- 统一解析逻辑 (Modbus: ushort[] 寄存器流) ---
                        if (dType == "BOOL" || dType == "BIT")
                        {
                            rawObjValue = bitValue;
                        }
                        else if (dType == "BYTE")
                        {
                            rawObjValue = (byte)(wordValue & 0xFF);
                        }
                        else if (dType == "SBYTE")
                        {
                            rawObjValue = (sbyte)(wordValue & 0xFF);
                        }
                        else if (dType == "INT16" || dType == "SHORT")
                        {
                            rawObjValue = (short)wordValue;
                        }
                        else if (dType == "UINT16" || dType == "WORD")
                        {
                            rawObjValue = wordValue;
                        }
                        else if (i + 1 < data.Length)
                        {
                            // 32 位处理 (默认 ABCD 大端序)
                            uint val32 = ((uint)data[i] << 16) | data[i + 1];
                            if (dType == "FLOAT" || dType == "REAL") rawObjValue = BitConverter.ToSingle(BitConverter.GetBytes(val32), 0);
                            else if (dType == "INT32" || dType == "DINT") rawObjValue = (int)val32;
                            else if (dType == "UINT32" || dType == "DWORD") rawObjValue = val32;
                        }

                        // 进入业务流水线
                        ExecutePipeline(p, rawObjValue, bitValue, "Modbus");
                    }
                }
            }
        }

        /// <summary>
        /// 专门处理字节流数据 (如 S7-PLC)
        /// </summary>
        public void ProcessByteStreamData(int sourceDeviceId, string startAddrTag, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            if (!_addressIndex.TryGetValue(sourceDeviceId, out var addrMap)) return;

            int startAddr = CommTaskGenerator.NormalizeAddress(startAddrTag);

            for (int i = 0; i < data.Length; i++)
            {
                int currentAddr = startAddr + i;
                if (addrMap.TryGetValue(currentAddr, out var affectedPoints))
                {
                    foreach (var p in affectedPoints)
                    {
                        byte byteVal = data[i];
                        bool bitValue = ((byteVal >> p.BitIndex) & 1) == 1;
                        object rawObjValue = byteVal;
                        string dType = p.DataType?.ToUpper() ?? "WORD";

                        // --- 统一解析逻辑 (S7: byte[] 字节流) ---
                        if (dType == "BOOL" || dType == "BIT")
                        {
                            rawObjValue = bitValue;
                        }
                        else if (dType == "BYTE")
                        {
                            rawObjValue = byteVal;
                        }
                        else if (dType == "SBYTE")
                        {
                            rawObjValue = (sbyte)byteVal;
                        }
                        else if (i + 1 < data.Length)
                        {
                            if (dType == "INT16" || dType == "UINT16" || dType == "WORD" || dType == "SHORT")
                            {
                                // 16位 (S7 默认为 Big-Endian)
                                ushort val16 = (ushort)((data[i] << 8) | data[i + 1]);
                                rawObjValue = (dType == "INT16" || dType == "SHORT") ? (object)(short)val16 : (object)val16;
                            }
                            else if (i + 3 < data.Length)
                            {
                                // 32位
                                uint val32 = ((uint)data[i] << 24) | ((uint)data[i + 1] << 16) | ((uint)data[i + 2] << 8) | (uint)data[i + 3];
                                if (dType == "FLOAT" || dType == "REAL") rawObjValue = BitConverter.ToSingle(BitConverter.GetBytes(val32), 0);
                                else if (dType == "INT32" || dType == "DINT") rawObjValue = (int)val32;
                                else if (dType == "UINT32" || dType == "DWORD") rawObjValue = val32;
                            }
                        }

                        ExecutePipeline(p, rawObjValue, bitValue, "S7");
                    }
                }
            }
        }

        /// <summary>
        /// 统一业务决策流水线 (Business Pipeline)
        /// 流程：1.变更检测 -> 2.更新缓存 -> 3.驱动UI -> 4.数据同步 -> 5.日志持久化
        /// </summary>
        private void ExecutePipeline(DevicePointConfigEntity p, object rawObjValue, bool bitValue, string protocol)
        {
            // 【核心降噪环节】：变位检测
            // 15,000 点位若全都触发后续逻辑，UI 会卡死。此处严格拦截任何未变化的数值。
            bool isChanged = false;

            if (p.LastValue == null)
            {
                isChanged = true;
            }
            else
            {
                // 分类型判定：布尔、双精度(带死区)、通用对象
                // 优化：避免不必要的装箱/拆箱和类型检查
                if (rawObjValue is bool bVal)
                {
                    isChanged = (bVal != (bool)(p.LastValue));
                }
                else if (rawObjValue is double dVal)
                {
                     isChanged = Math.Abs(dVal - (double)(p.LastValue)) > 0.000001; 
                }
                else
                {
                     isChanged = !rawObjValue.Equals(p.LastValue);
                }
            }

            // 拦截无效更新
            if (!isChanged) return;

            // 1. 更新运行时快照
            p.LastValue = rawObjValue;
            
            // 2. 根据全局 Debug 开关判定是否打印追踪日志
            // 【性能优化】：必须先判断 shouldLog 再进行字符串拼接，否则 1.5 万点位的字符串构造会拖垮 CPU
            if (GlobalData.DebugConfig != null)
            {
                bool shouldLog = false;
                if (protocol == "S7") shouldLog = GlobalData.DebugConfig.Trace_S7_Detail;
                else if (protocol == "Modbus") shouldLog = GlobalData.DebugConfig.Trace_Modbus_Detail;
                else shouldLog = GlobalData.DebugConfig.Trace_Communication_Raw;

                if (shouldLog && p.UiBinding != null)
                {
                    LogHelper.Debug($"[{protocol}] Update: {p.UiBinding} = {rawObjValue}");
                }
            }

            // 3. 驱动 UI 状态机 (仅针对绑定了 UI 的点位执行，TargetType.None 直接忽略)
            // 这是降低 UI 线程 CPU 占用的关键：不让非 UI 点位进入刷新队列
            if (p.TargetType != TargetType.None)
            {
                DataManager.Instance.UpdatePointValue(p.TargetKeyId, p.TargetBitConfigKeyId, p.TargetType, bitValue);
            }

            // 4. 执行点位联动转发
            if (p.IsSyncEnabled)
            {
                DataSyncService.SyncData(p, rawObjValue, bitValue, _runtimes, _slaves);
            }

            // 5. 提交日志持久化任务 (异步队列处理)
            LogService.Instance.ProcessLogging(p, bitValue, rawObjValue);
        }
    }
}
