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
    /// 数据处理引擎 (核心处理核心)
    /// 负责：
    /// 1. 将通讯层收到的原始字节(Byte)或线圈(Ushort)切片，提取出具体位和多字节类型。
    /// 2. 触发业务更新流水线 (UI映射 -> 转发同步 -> 日志存储)。
    /// </summary>
    public class DataProcessor
    {
        private readonly IDictionary<int, CommRuntime> _runtimes;
        private readonly IDictionary<int, ICommBase> _slaves;
        private readonly IDictionary<int, List<DevicePointConfigEntity>> _devicePointsCache;

        /// <summary>
        /// 构造函数需传入设备缓存引用
        /// </summary>
        public DataProcessor(
            IDictionary<int, CommRuntime> runtimes, 
            IDictionary<int, ICommBase> slaves, 
            IDictionary<int, List<DevicePointConfigEntity>> devicePointsCache)
        {
            _runtimes = runtimes;
            _slaves = slaves;
            _devicePointsCache = devicePointsCache;
        }

        /// <summary>
        /// 处理数据入口
        /// </summary>
        /// <param name="sourceDeviceId">源设备ID</param>
        /// <param name="startAddrTag">本次采集块的起始地址描述符</param>
        /// <param name="rawData">原始数据 (Modbus对应ushort[], S7对应byte[])</param>
        public void ProcessData(int sourceDeviceId, string startAddrTag, object rawData)
        {
            // 路由：根据原始数据类型分发解析逻辑
            if (rawData is byte[] rawBytes)
            {
                ProcessByteStreamData(sourceDeviceId, startAddrTag, rawBytes);
                return;
            }

            ushort[]? data = rawData as ushort[];
            if (data == null || data.Length == 0) return;

            // 1. 归一化地址偏移，找到数据包的首个索引
            int startAddr = CommTaskGenerator.NormalizeAddress(startAddrTag);
            if (!_devicePointsCache.TryGetValue(sourceDeviceId, out var points)) return;

            // 2. 遍历该设备下受影响的点位 (高性能映射)
            foreach (var p in points)
            {
                int targetAddr = CommTaskGenerator.NormalizeAddress(p.Address);
                int offset = targetAddr - startAddr;
                
                // 检查点位地址是否落在当前这个数据块中
                if (offset < 0 || offset >= data.Length) continue;

                ushort wordValue = data[offset];
                bool bitValue = ((wordValue >> p.BitIndex) & 1) == 1;
                object rawObjValue = wordValue; 
                string dType = p.DataType?.ToLower() ?? "word";

                // --- 情况 A: 32位数据提取 (DWord, Float等需要读取两个寄存器) ---
                if ((dType.Contains("dword") || dType.Contains("int32") || dType.Contains("integer") || dType.Contains("float") || dType.Contains("real")) 
                    && (offset + 1 < data.Length))
                {
                    uint high = data[offset];
                    uint low = data[offset + 1];
                    uint dwordVal = (high << 16) | low; // 默认 CD AB (Big-Endian Word Order)

                    if (dType.Contains("float") || dType.Contains("real")) rawObjValue = BitConverter.ToSingle(BitConverter.GetBytes(dwordVal), 0);
                    else if (dType.Contains("int32") || dType.Contains("integer")) rawObjValue = (int)dwordVal;
                    else rawObjValue = dwordVal;
                }
                // --- 情况 B: 16位有符号数 ---
                else if (dType.Contains("int16") || dType.Contains("short")) rawObjValue = (short)wordValue;
                // --- 情况 C: 8位字节数据 ---
                else if (dType.Contains("byte")) rawObjValue = (byte)(wordValue & 0xFF);
                // --- 情况 D: 布尔位 ---
                else if (dType.Contains("bit") || dType.Contains("bool")) rawObjValue = bitValue;

                // 3. 执行业务流水线 (传入协议类型用于日志分类)
                ExecutePipeline(p, rawObjValue, bitValue, "Modbus");
            }
        }

        /// <summary>
        /// 专门处理字节流数据 (如 S7-PLC 的 DB 块原始字节)
        /// </summary>
        public void ProcessByteStreamData(int sourceDeviceId, string startAddrTag, byte[] data)
        {
            if (data == null || data.Length == 0) return;

            int startAddr = CommTaskGenerator.NormalizeAddress(startAddrTag);
            if (!_devicePointsCache.TryGetValue(sourceDeviceId, out var points)) return;

            foreach (var p in points)
            {
                int targetAddr = CommTaskGenerator.NormalizeAddress(p.Address);
                int offset = targetAddr - startAddr;
                if (offset < 0 || offset >= data.Length) continue;

                byte byteVal = data[offset];
                bool bitValue = ((byteVal >> p.BitIndex) & 1) == 1;
                object rawObjValue = byteVal;
                string dType = p.DataType?.ToLower() ?? "word";

                // --- 复合数据解析 (考虑到 Siemens S7 是大端序) ---
                if ((dType.Contains("dword") || dType.Contains("int32") || dType.Contains("integer") || dType.Contains("float") || dType.Contains("real")) 
                    && (offset + 3 < data.Length))
                {
                    byte[] slice = new byte[4];
                    Array.Copy(data, offset, slice, 0, 4);
                    Array.Reverse(slice); // 翻转字节顺序以匹配 C# 的小端序环境
                    
                    if (dType.Contains("float") || dType.Contains("real")) rawObjValue = BitConverter.ToSingle(slice, 0);
                    else if (dType.Contains("int32") || dType.Contains("integer")) rawObjValue = BitConverter.ToInt32(slice, 0);
                    else rawObjValue = BitConverter.ToUInt32(slice, 0);
                }
                else if (dType.Contains("int16") || dType.Contains("short") || dType.Contains("word"))
                {
                    if (offset + 1 < data.Length)
                    {
                        byte[] slice = new byte[2] { data[offset+1], data[offset] }; // S7 习惯
                        rawObjValue = BitConverter.ToUInt16(slice, 0);
                    }
                }
                else if (dType.Contains("bit") || dType.Contains("bool")) rawObjValue = bitValue;

                ExecutePipeline(p, rawObjValue, bitValue, "S7");
            }
        }

        /// <summary>
        /// 统一业务决策流水线：
        /// 数据解析出来后，必须按顺序下达三个指令
        /// </summary>
        private void ExecutePipeline(DevicePointConfigEntity p, object rawObjValue, bool bitValue, string protocol)
        {
            // 0. 变更检测 (Change Detection) - 核心性能优化
            // 防止重复的数值反复触发 UI 更新、日志检查和同步逻辑
            bool isChanged = false;

            if (p.LastValue == null)
            {
                isChanged = true;
            }
            else
            {
                // 根据类型进行比对
                if (rawObjValue is bool bVal && p.LastValue is bool bLast)
                {
                    isChanged = (bVal != bLast);
                }
                else if (rawObjValue is double dVal && p.LastValue is double dLast)
                {
                     // 简单浮点比对，如果需要死区应在 LogService 处理，这里仅关注是否完全一致以决定是否重绘 UI
                     isChanged = Math.Abs(dVal - dLast) > 0.000001; 
                }
                else
                {
                    // 通用比对 (Int, String, etc)
                    isChanged = !rawObjValue.Equals(p.LastValue);
                }
            }

            // 如果数值不仅没变，而且不是第一次读取，则直接忽略，极大降低 CPU 占用
            if (!isChanged) return;

            // 1. 更新实时值缓存 (用于参数界面回显)
            // 注意：这会触发 PropertyChanged，如果绑定了界面也会刷新
            p.LastValue = rawObjValue;
            
            // 降噪与分类日志：根据协议开启对应的详细跟踪
            bool shouldLog = false;
            if (GlobalData.DebugConfig != null)
            {
                if (protocol == "S7") shouldLog = GlobalData.DebugConfig.Trace_S7_Detail;
                else if (protocol == "Modbus") shouldLog = GlobalData.DebugConfig.Trace_Modbus_Detail;
                else shouldLog = GlobalData.DebugConfig.Trace_Communication_Raw; // 降级到 Raw 开关
            }

            if (p.UiBinding != null && shouldLog)
            {
                 LogHelper.Debug($"[{protocol}] Set LastValue for {p.UiBinding} = {rawObjValue} (ObjHash: {p.GetHashCode()})");
            }

            // 2. 发射到 UI DataManager：让全局画面动起来
            DataManager.Instance.UpdatePointValue(p.TargetObjId, p.TargetBitConfigId, p.TargetType, bitValue);

            // 3. 发射到转发引擎：如果点位开启了转发，则尝试操作目标设备
            if (p.IsSyncEnabled)
            {
                DataSyncService.SyncData(p, rawObjValue, bitValue, _runtimes, _slaves);
            }

            // 4. 发射到日志引擎
            // LogService 内部也有去重逻辑，但在这里拦截可以减少方法调用开销
            LogService.Instance.ProcessLogging(p, bitValue, rawObjValue);
        }
    }
}
