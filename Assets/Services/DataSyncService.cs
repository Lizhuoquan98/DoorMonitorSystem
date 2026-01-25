using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using Communicationlib.TaskEngine;
using DoorMonitorSystem;
using Communicationlib.Core;
using Communicationlib; // 包含 ICommBase

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 数据同步与转发服务
    /// 核心职责：将采集到的变位数据同步到其他目标设备（例如：转发主站数据到本地从站，或同步到另一台 PLC）。
    /// </summary>
    public static class DataSyncService
    {
        /// <summary>
        /// 执行同步转发请求
        /// </summary>
        /// <param name="config">源点位配置 (包含同步目标信息)</param>
        /// <param name="rawValue">提取出的原始数值对象</param>
        /// <param name="bitValue">布尔状态</param>
        /// <param name="runtimes">当前运行的主站连接缓存</param>
        /// <param name="slaves">当前运行的从站/服务端缓存</param>
        public static void SyncData(
            DevicePointConfigEntity config, 
            object rawValue, 
            bool bitValue, 
            IDictionary<int, CommRuntime> runtimes, 
            IDictionary<int, ICommBase> slaves)
        {
            try
            {
                // 如果没有配置同步目标，则直接退出
                if (!config.SyncTargetDeviceId.HasValue || !config.SyncTargetAddress.HasValue) return;

                int targetDevId = config.SyncTargetDeviceId.Value;
                string targetAddrStr = config.SyncTargetAddress.Value.ToString();
                int targetAddrNormalized = CommTaskGenerator.NormalizeAddress(targetAddrStr);
                string dType = config.DataType?.ToLower() ?? "word";

                // --- 日志追踪 ---
                bool trace = GlobalData.DebugConfig?.Trace_Forwarding_Detail ?? false;
                if (trace)
                {
                    LogHelper.Debug($"[Sync] {config.Description} (Src->Dst): [Dev:{config.SourceDeviceId} @ {config.Address}] -> [Dev:{targetDevId} @ {targetAddrStr}] Val:{rawValue}");
                }

                // 1. 数据对齐：将原始值转换为适合网络传输的寄存器数组 (ushort[])
                ushort[]? registersToWrite = null;
                bool isBitMode = false;

                if (dType.Contains("dword") || dType.Contains("int32") || dType.Contains("uint32") || dType.Contains("integer"))
                {
                     // Fix: Use Int64 first to safely handle negative Int32 values without overflow, then cast to UInt32
                    long lVal = Convert.ToInt64(rawValue);
                    uint val = (uint)lVal;
                    
                    ushort high = (ushort)(val >> 16);
                    ushort low = (ushort)(val & 0xFFFF);
                   
                    registersToWrite = new ushort[] { high, low }; // ABCD Format (High Word First)
                    if (trace) LogHelper.Debug($"    -> Convert Int32: [{high:X4}, {low:X4}]");
                }
                else if (dType.Contains("float") || dType.Contains("real"))
                {
                    float fVal = Convert.ToSingle(rawValue);
                    byte[] bytes = BitConverter.GetBytes(fVal);
                    // BitConverter returns Little Endian on Windows (Low byte @ 0).
                    ushort low = BitConverter.ToUInt16(bytes, 0);
                    ushort high = BitConverter.ToUInt16(bytes, 2);
                    
                    // Fix: Align with Integer behavior -> Send High Word First (ABCD)
                    // Previous: { low, high } (CDAB) -> Inconsistent
                    registersToWrite = new ushort[] { high, low }; 
                    if (trace) LogHelper.Debug($"    -> Convert Float: [{high:X4}, {low:X4}]");
                }
                else if (dType.Contains("word") || dType.Contains("int16") || dType.Contains("short"))
                {
                    ushort val = Convert.ToUInt16(rawValue);
                    registersToWrite = new ushort[] { val };
                    if (trace) LogHelper.Debug($"    -> Convert Word: [{val:X4}]");
                }
                else
                {
                    // 默认为位操作模式
                    isBitMode = true;
                }

                // 2. 位状态转换逻辑 (SyncMode 1 表示取反)
                int targetBitIndex = config.SyncTargetBitIndex ?? config.BitIndex;
                bool finalBitValue = (config.SyncMode == 1) ? !bitValue : bitValue;
                bool handled = false;

                // --- 情况 A: 目标是本地 Slave (通常用于上位机对外提供数据源) ---
                if (slaves.TryGetValue(targetDevId, out var slave))
                {
                    // 目前仅支持 ModbusTcpSlave 类型的内存操作
                    if (slave is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave modbusSlave && modbusSlave.Memory != null)
                    {
                        ushort tAddr = (ushort)targetAddrNormalized;
                        try
                        {
                            if (!isBitMode && registersToWrite != null)
                            {
                                // 批量寄存器写入
                                for (int i = 0; i < registersToWrite.Length; i++)
                                    modbusSlave.Memory.WriteSingleRegister((ushort)(tAddr + i), registersToWrite[i]);
                                handled = true;
                            }
                            else
                            {
                                // 位聚合写入 (Read-Modify-Write)
                                var registers = modbusSlave.Memory.ReadHoldingRegisters(tAddr, 1);
                                if (registers != null && registers.Length > 0)
                                {
                                    ushort currentVal = registers[0];
                                    ushort mask = (ushort)(1 << targetBitIndex);
                                    ushort newVal = finalBitValue ? (ushort)(currentVal | mask) : (ushort)(currentVal & ~mask);
                                    modbusSlave.Memory.WriteSingleRegister(tAddr, newVal);
                                    handled = true;
                                }
                            }
                        }
                       
                        catch (Exception ex) { LogHelper.Error($"[Sync] 本地从站内存写入失败: {ex.Message}"); }
                        
                        // if (handled && trace) LogHelper.Debug($"[Sync] Local Write Success: Dev:{targetDevId} Addr:{tAddr}");
                    }
                }

                // --- 情况 B: 目标是远程设备 Runtime (Client 模式下的双机同步) ---
                if (!handled && runtimes.TryGetValue(targetDevId, out var runtime))
                {
                    var targetDevConfig = GlobalData.ListDveices.FirstOrDefault(d => d.ID == targetDevId);
                    bool isS7Target = targetDevConfig?.Protocol?.Contains("S7") == true;

                    // 异步发送写入命令，防止阻塞当前数据解析流水线
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (!isBitMode && registersToWrite != null)
                            {
                                if (isS7Target)
                                {
                                    // S7协议转换：寄存器组转为大端序字节数组
                                    var byteList = new List<byte>();
                                    foreach (var reg in registersToWrite) { byteList.Add((byte)(reg >> 8)); byteList.Add((byte)(reg & 0xFF)); }
                                    await runtime.WriteAsync(targetAddrStr, byteList.ToArray());
                                }
                                else
                                {
                                    // Modbus 协议写入
                                    if (registersToWrite.Length == 1) await runtime.WriteAsync(targetAddrStr, registersToWrite[0]);
                                    else await runtime.WriteAsync(targetAddrStr, registersToWrite);
                                }
                            }
                            else
                            {
                                // 远程位状态同步
                                var readResult = await runtime.ReadAsync(targetAddrStr, 1);
                                if (readResult != null && readResult.Length > 0)
                                {
                                    ushort currentVal = readResult[0];
                                    ushort mask = (ushort)(1 << targetBitIndex);
                                    ushort newVal = finalBitValue ? (ushort)(currentVal | mask) : (ushort)(currentVal & ~mask);
                                    if (isS7Target) await runtime.WriteAsync(targetAddrStr, new byte[] { (byte)(newVal >> 8), (byte)(newVal & 0xFF) });
                                    else await runtime.WriteAsync(targetAddrStr, newVal);
                                }
                            }
                            
                            if (trace) LogHelper.Debug($"[Sync] Remote Write Sent: Dev:{targetDevId} Addr:{targetAddrStr}");
                        }
                        catch (Exception ex) { LogHelper.Error($"[Sync] 远程设备同步失败 (ID:{targetDevId}): {ex.Message}"); }
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[Sync] 引擎运行异常: {ex.Message}"); }
        }
    }
}
