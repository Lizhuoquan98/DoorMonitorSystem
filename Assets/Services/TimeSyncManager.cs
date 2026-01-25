using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Communicationlib;
using Communicationlib.config;
using DoorMonitorSystem.Assets.Helper;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 设备校时管理器
    /// 负责管理所有设备的对时任务 (PC->PLC, PLC->PC)
    /// </summary>
    public class TimeSyncManager : IDisposable
    {
        private static TimeSyncManager? _instance;
        public static TimeSyncManager Instance => _instance ??= new TimeSyncManager();

        private readonly ConcurrentDictionary<int, Timer> _timers = new ConcurrentDictionary<int, Timer>();
        private readonly ConcurrentDictionary<int, DateTime> _nextFixedRunTime = new ConcurrentDictionary<int, DateTime>();
        private bool _isRunning = false;

        #region Win32 API (For Setting Time)
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetLocalTime(ref SYSTEMTIME lpSystemTime);
        #endregion

        // 引用 DeviceCommunicationService 中的设备列表
        // 实际上我们可以只保存 ConfigEntity 和 ICommBase 的引用
        // 引用 DeviceCommunicationService 中的设备列表
        // 兼容 ICommBase (Server) 和 CommRuntime (Client)
        private Dictionary<ConfigEntity, object> _tasks = new Dictionary<ConfigEntity, object>();

        /// <summary>
        /// 初始化并启动校时服务
        /// </summary>
        /// <param name="devices">配置实体与通讯对象的映射字典</param>
        public void Start(Dictionary<ConfigEntity, object> devices)
        {
            Stop();
            _tasks = devices ?? new Dictionary<ConfigEntity, object>();
            _isRunning = true;

            foreach (var kvp in _tasks)
            {
                var cfg = kvp.Key;
                var deviceObj = kvp.Value;

                if (cfg.TimeSync == null || !cfg.TimeSync.Enabled) continue;

                // 启动每个设备的调度任务
                StartDeviceTask(cfg, deviceObj);
            }
            LogHelper.Info($"[TimeSync] 校时服务已启动，监控 {devices.Count} 个设备");
        }

        /// <summary>
        /// 停止所有校时任务并释放资源
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            foreach (var t in _timers.Values) t.Dispose();
            _timers.Clear();
            _nextFixedRunTime.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 为单个设备启动独立的校时/监控任务
        /// </summary>
        /// <param name="cfg">设备配置实体</param>
        /// <param name="deviceObj">设备通讯对象 (ICommBase 或 CommRuntime)</param>
        private void StartDeviceTask(ConfigEntity cfg, object deviceObj)
        {
            // 模式判定
            // 如果是 "受时" (Upstream, Direction=1)，我们需要高频检测触发信号，否则会有长达数十秒的误差
            if (cfg.TimeSync.Direction == 1)
            {
                // 快速轮询模式 (固定 500ms 或 200ms)
                // 本机内存或者局域网读取通常很快，不会造成负担
                // 目的：为了及时响应 PLC 的写操作，减少同步延迟
                int fastInterval = 500; 
                var timer = new Timer(async _ => await ExecuteSyncTaskSafe(cfg, deviceObj), null, 2000, fastInterval);
                _timers[cfg.ID] = timer;
                LogHelper.Info($"[TimeSync] 设备 {cfg.Name} (ID:{cfg.ID}) 启动信号监控模式 (轮询间隔: {fastInterval}ms)");
            }
            else if (cfg.TimeSync.ScheduleMode == 0) // Interval (Downstream)
            {
                // 周期下发模式
                int interval = Math.Max(10, cfg.TimeSync.IntervalSeconds) * 1000;
                var timer = new Timer(async _ => await ExecuteSyncTaskSafe(cfg, deviceObj), null, 5000, interval);
                _timers[cfg.ID] = timer;
                LogHelper.Info($"[TimeSync] 设备 {cfg.Name} (ID:{cfg.ID}) 周期下发任务启动: {cfg.TimeSync.IntervalSeconds}秒");
            }
            else // FixedTime (Downstream)
            {
                // 固定时间模式，每分钟检查一次是否到达时间
                var timer = new Timer(async _ => await CheckFixedTimeTask(cfg, deviceObj), null, 5000, 60000);
                _timers[cfg.ID] = timer;
                
                // 计算下一次执行时间
                CalculateNextRunTime(cfg);
                
                LogHelper.Info($"[TimeSync] 设备 {cfg.Name} (ID:{cfg.ID}) 定点下发任务启动: {cfg.TimeSync.FixedTime}");
            }
        }

        private void CalculateNextRunTime(ConfigEntity cfg)
        {
            if (DateTime.TryParse(cfg.TimeSync.FixedTime, out DateTime targetTime))
            {
                var now = DateTime.Now;
                var todayTarget = new DateTime(now.Year, now.Month, now.Day, targetTime.Hour, targetTime.Minute, targetTime.Second);
                
                if (todayTarget > now)
                    _nextFixedRunTime[cfg.ID] = todayTarget;
                else
                    _nextFixedRunTime[cfg.ID] = todayTarget.AddDays(1);
            }
        }

        private async Task CheckFixedTimeTask(ConfigEntity cfg, object deviceObj)
        {
            if (!_nextFixedRunTime.TryGetValue(cfg.ID, out var nextRun)) return;

            if (DateTime.Now >= nextRun)
            {
                await ExecuteSyncTaskSafe(cfg, deviceObj);
                // 更新下一次时间 (+1天)
                _nextFixedRunTime[cfg.ID] = nextRun.AddDays(1);
            }
        }

        private async Task ExecuteSyncTaskSafe(ConfigEntity cfg, object deviceObj)
        {
            if (!_isRunning) return;
            try
            {
                await ExecuteSync(cfg, deviceObj);
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[TimeSync] 设备 {cfg.Name} 校时任务异常", ex);
            }
        }

        /// <summary>
        /// 执行核心同步逻辑
        /// </summary>
        private async Task ExecuteSync(ConfigEntity cfg, object deviceObj)
        {
            // 兼容性判断：检查设备是否连接
            bool isConnected = false;
            if (deviceObj is ICommBase comm) isConnected = comm.IsConnected;
            else if (deviceObj is Communicationlib.TaskEngine.CommRuntime runtime) isConnected = runtime.IsRunning; 
            
            // 如果该设备处于断线状态，跳过本次同步
            if (!isConnected) return;
            
            var syncCfg = cfg.TimeSync;
            
            // 模式分支
            if (syncCfg.Direction == 0) // PC -> PLC
            {
                await ExecuteDownstreamSync(cfg, deviceObj, syncCfg);
            }
            else // PLC -> PC (本机受时)
            {
                await ExecuteUpstreamSync(cfg, commObj: null, deviceObj, syncCfg);
            }
        }

        /// <summary>
        /// 执行上行同步 (PLC -> PC)
        /// 1. 监控触发地址的值
        /// 2. 当触发值匹配时，批量读取 PLC 的时间数据
        /// 3. 调用 Win32 API 修改本机系统时间
        /// </summary>
        private async Task ExecuteUpstreamSync(ConfigEntity cfg, object commObj, object deviceObj, TimeSyncConfig syncCfg)
        {
            // 1. 检查触发信号 (Trigger Check)
            if (!string.IsNullOrEmpty(syncCfg.TriggerAddress) && ushort.TryParse(syncCfg.TriggerAddress, out ushort trigAddr))
            {
                // 读取触发位
                ushort[] trigVal = await ReadRegisters(deviceObj, trigAddr, 1);
                if (trigVal == null || trigVal.Length == 0) return;

                int currentVal = trigVal[0];
                int targetVal = syncCfg.TriggerValue;

                // 只有当值匹配时才同步
                if (currentVal == targetVal)
                {
                    LogHelper.Info($"[TimeSync] <- Device {cfg.Name} (Upstream) 触发信号激活 ({currentVal})，开始同步本机时间...");
                    
                    try 
                    {
                        // 2. 批量读取时间寄存器 (优化：一次读取所有时间相关寄存器，防止跨秒不一致，且减少IO耗时)
                        // 解析所有地址设为列表
                        var addrMap = new List<(string Name, ushort Addr)>();
                        if(ushort.TryParse(syncCfg.AddressYear, out ushort aYear)) addrMap.Add(("Year", aYear));
                        if(ushort.TryParse(syncCfg.AddressMonth, out ushort aMonth)) addrMap.Add(("Month", aMonth));
                        if(ushort.TryParse(syncCfg.AddressDay, out ushort aDay)) addrMap.Add(("Day", aDay));
                        if(ushort.TryParse(syncCfg.AddressHour, out ushort aHour)) addrMap.Add(("Hour", aHour));
                        if(ushort.TryParse(syncCfg.AddressMinute, out ushort aMinute)) addrMap.Add(("Minute", aMinute));
                        if(ushort.TryParse(syncCfg.AddressSecond, out ushort aSecond)) addrMap.Add(("Second", aSecond));

                        if (addrMap.Count == 0) return;

                        // 计算最小包围盒 (Min Bounding Box)
                        ushort minAddr = addrMap.Min(x => x.Addr);
                        ushort maxAddr = addrMap.Max(x => x.Addr);
                        ushort count = (ushort)(maxAddr - minAddr + 1);

                        // 限制合理的批量读取大小 (例如不超过 100个寄存器)
                        if (count > 100)
                        {
                            LogHelper.Warn($"[TimeSync] 地址跨度太大 ({count})，建议优化地址配置，使其尽量连续。");
                        }

                        // 执行批量读取，原子操作
                        ushort[] dataBlock = await ReadRegisters(deviceObj, minAddr, count);
                        if (dataBlock == null || dataBlock.Length != count) 
                        {
                             LogHelper.Warn($"[TimeSync] 批量读取失败，期待 {count} 个，实际获取 {(dataBlock?.Length ?? 0)} 个");
                             return;
                        }

                        // 从 Block 中提取数据 (根据偏移量)
                        Func<string, int, int> getVal = (name, def) => 
                        {
                            var item = addrMap.FirstOrDefault(x => x.Name == name);
                            if (item == default) return def;
                            int offset = item.Addr - minAddr;
                            // 确保索引安全
                            if (offset >= 0 && offset < dataBlock.Length) return dataBlock[offset];
                            return def;
                        };

                        // 构造时间对象
                        var timeData = new DateTime(
                            getVal("Year", DateTime.Now.Year),
                            getVal("Month", DateTime.Now.Month),
                            getVal("Day", DateTime.Now.Day),
                            getVal("Hour", DateTime.Now.Hour),
                            getVal("Minute", DateTime.Now.Minute),
                            0
                        );

                        int secVal = getVal("Second", DateTime.Now.Second);
                        if (syncCfg.SecondFormat == 1) // 毫秒模式
                        {
                            timeData = timeData.AddMilliseconds(secVal);
                        }
                        else // 秒模式
                        {
                            timeData = timeData.AddSeconds(secVal);
                        }

                        // 应用手动补偿 (OffsetMs)
                        //if (syncCfg.OffsetMs != 0)
                        //{
                        //    timeData = timeData.AddMilliseconds(syncCfg.OffsetMs);
                        //}

                        // 3. 设置本机时间
                        LogHelper.Info($"[TimeSync] <- Device {cfg.Name} 读取时间: {timeData:yyyy-MM-dd HH:mm:ss}");
                        if (ApplySystemTime(timeData))
                        {
                            LogHelper.Info($"[TimeSync] 本机时间已更新成功。");
                            
                            // 4. 清除 PLC 下发的数据 (Handshake & Data Clear)
                            // 用户要求：不仅复位 Trigger，还要清掉时间数据，防止重复读取导致时间卡死
                            try 
                            {
                                var clearList = new List<ushort>();
                                // 添加所有需要清除的地址
                                clearList.Add(trigAddr);
                                clearList.AddRange(addrMap.Select(x => x.Addr));
                                
                                // 去重并排序
                                var uniqueAddrs = clearList.Distinct().OrderBy(x => x).ToList();

                                // 批量或逐个清除
                                foreach (var addr in uniqueAddrs)
                                {
                                    if (deviceObj is Communicationlib.TaskEngine.CommRuntime rt)
                                    {
                                        await rt.WriteAsync(addr.ToString(), (ushort)0);
                                    }
                                    else if (deviceObj is ICommBase cb)
                                    {
                                        if (cb is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave slave)
                                        {
                                            // 本地直接写内存
                                            slave.Memory.WriteSingleRegister(addr, 0);
                                        }
                                        else
                                        {
                                            await WriteSingleRegister(cb, (byte)1, addr, 0); 
                                        }
                                    }
                                }
                                LogHelper.Info($"[TimeSync] <- 已清除 PLC 下发的时间数据和触发信号。");
                            }
                            catch (Exception ex) 
                            {
                                LogHelper.Warn($"[TimeSync] 清除 PLC 数据失败: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"[TimeSync] <- Device {cfg.Name} 读取时间失败: {ex.Message}");
                    }
                }
            }
        }
        
        private async Task<int> ReadVal(object deviceObj, string addrStr, int defaultVal)
        {
             if (string.IsNullOrWhiteSpace(addrStr) || !ushort.TryParse(addrStr, out ushort addr)) return defaultVal;
             var res = await ReadRegisters(deviceObj, addr, 1);
             return (res != null && res.Length > 0) ? res[0] : defaultVal;
        }

        private async Task<ushort[]> ReadRegisters(object deviceObj, ushort addr, ushort count)
        {
            try
            {
                if (deviceObj is Communicationlib.TaskEngine.CommRuntime rt)
                {
                    string addrS = addr.ToString(); 
                    return await rt.ReadAsync(addrS, count);
                }
                else if (deviceObj is ICommBase cb)
                {
                    if (cb is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave slave)
                    {
                         return slave.Memory.ReadHoldingRegisters(addr, count).ToArray(); 
                    }
                }
            }
            catch {}
            return null;
        }

        private bool ApplySystemTime(DateTime dt)
        {
            SYSTEMTIME st = new SYSTEMTIME
            {
                wYear = (ushort)dt.Year,
                wMonth = (ushort)dt.Month,
                wDay = (ushort)dt.Day,
                wHour = (ushort)dt.Hour,
                wMinute = (ushort)dt.Minute,
                wSecond = (ushort)dt.Second,
                wMilliseconds = (ushort)dt.Millisecond
            };
            return SetLocalTime(ref st);
        }

        /// <summary>
        /// 执行下行同步 (PC -> PLC)
        /// 支持单台、广播、范围轮询多种策略
        /// </summary>
        private async Task ExecuteDownstreamSync(ConfigEntity cfg, object deviceObj, TimeSyncConfig syncCfg)
        {
            // 确定目标 Slave IDs
            List<int> targetSlaveIds = new List<int>();

            // 策略判定
            if (syncCfg.Strategy == 1) // Broadcast (广播)
            {
                targetSlaveIds.Add(syncCfg.BroadcastStationId > 0 ? syncCfg.BroadcastStationId : 255);
            }
            else if (syncCfg.Strategy == 2) // Range Poll (范围轮询)
            {
                targetSlaveIds = ParseSlaveIdRange(syncCfg.SlaveIdRange);
            }
            else // Default Self (单机默认)
            {
                // 默认取 CommParams 中的 SlaveId (通常叫 "SlaveId" 或者 "StationId")
                int selfId = GetDeviceSlaveId(cfg);
                targetSlaveIds.Add(selfId);
            }

            // 执行下发
            foreach (var slaveId in targetSlaveIds)
            {
                bool success = false;
                // 重试逻辑
                for (int i = 0; i < Math.Max(1, syncCfg.RetryCount); i++)
                {
                    if (await WriteTimeToDevice(deviceObj, (byte)slaveId, syncCfg))
                    {
                        success = true;
                        break;
                    }
                    await Task.Delay(500); // Retry delay
                }

                if (success)
                {
                   if (syncCfg.Strategy != 2) // 如果不是大量轮询，就记录一下日志
                       LogHelper.Info($"[TimeSync] -> Device {cfg.Name} (Slave {slaveId}) 校时成功。");
                }
                else
                {
                    // 广播模式下没有回复，WriteTimeToDevice 只有在发不出去时才返回 false，所以这里失败是真的失败
                    LogHelper.Warn($"[TimeSync] -> Device {cfg.Name} (Slave {slaveId}) 校时失败 (尝试 {syncCfg.RetryCount} 次)。");
                }
            }
            
            if (syncCfg.Strategy == 2)
                 LogHelper.Info($"[TimeSync] -> Device {cfg.Name} (Range) 批量校时完成。");
        }

        private async Task<bool> WriteTimeToDevice(object deviceObj, byte slaveId, TimeSyncConfig cfg)
        {
            try
            {
                var now = DateTime.Now;
                
                // 1. 写入时间数据
                
                var writes = new List<(ushort addr, ushort val)>();
                
                AddWriteTask(writes, cfg.AddressYear, now.Year);
                AddWriteTask(writes, cfg.AddressMonth, now.Month);
                AddWriteTask(writes, cfg.AddressDay, now.Day);
                AddWriteTask(writes, cfg.AddressHour, now.Hour);
                AddWriteTask(writes, cfg.AddressMinute, now.Minute);
                
                // 处理秒/毫秒
                int secValue = now.Second;
                if (cfg.SecondFormat == 1) // Milliseconds Mode
                {
                    secValue = now.Second * 1000 + now.Millisecond;
                }
                AddWriteTask(writes, cfg.AddressSecond, secValue);

                if (writes.Count == 0) return true; // Nothing to write

                // 排序
                writes.Sort((a, b) => a.addr.CompareTo(b.addr));


                // 执行写入动作
                if (deviceObj is ICommBase comm)
                {
                     // Server (ICommBase) 路径
                     await PerformWriteICommBase(comm, slaveId, writes, cfg);
                }
                else if (deviceObj is Communicationlib.TaskEngine.CommRuntime runtime)
                {
                    // Client (CommRuntime) 路径
                    // 注意：CommRuntime.WriteAsync 仅支持单值。我们需要判断是否需要批量。
                    // 且 CommRuntime 屏蔽了底层细节。对于 Modbus TcpClient，WriteAsync 底层会自动选择功能码。
                    // 但是 TimeSync 策略要求比较细（如 FC06 强制）。CommRuntime 现有的 WriteAsync 比较简单。
                    // 暂时我们循环调用 WriteAsync。
                    // 另外，CommRuntime.WriteAsync 接受的是 "地址字符串" (如 "40001")。
                    // 这里的 cfg.AddressYear 已经是字符串了，所以我们应该直接用字符串。
                    // 上面的 writes 解析成了 ushort addr，对于 Runtime 来说，反而不是很好用。
                    // 让我们重构一下，直接操作 Config 里的字符串。
                    
                    await PerformWriteRuntime(runtime, slaveId, cfg, now);
                }

                return true;
            }
            catch (Exception ex)
            {
                if (cfg.Strategy == 1 || slaveId == 255 || slaveId == 0) return true; 
                LogHelper.Debug($"[TimeSync] Write failed: {ex.Message}");
                return false;
            }
        }

        private async Task PerformWriteICommBase(ICommBase comm, byte slaveId, List<(ushort addr, ushort val)> writes, TimeSyncConfig cfg)
        {
             // 优化：如果是本地 ModbusTcpSlave，直接写内存，绕过 Socket 回环
             if (comm is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave slave)
             {
                 try
                 {
                     foreach (var w in writes)
                     {
                         // 直接写内存 (同步方法，但很快)
                         slave.Memory.WriteSingleRegister(w.addr, w.val);
                     }
                     
                     // 触发/握手
                     if (!string.IsNullOrEmpty(cfg.TriggerAddress) && ushort.TryParse(cfg.TriggerAddress, out ushort trigAddrLocal))
                     {
                         slave.Memory.WriteSingleRegister(trigAddrLocal, (ushort)cfg.TriggerValue);
                         if (cfg.IsTriggerPulse)
                         {
                             await Task.Delay(500);
                             slave.Memory.WriteSingleRegister(trigAddrLocal, 0);
                         }
                     }
                     return;
                 }
                 catch (Exception ex)
                 {
                     LogHelper.Warn($"[TimeSync] 本地内存写入失败: {ex.Message}");
                     return; 
                 }
             }

             // 远程设备 or 其他协议，走标准流程
             if (cfg.ForceSingleWrite)
                {
                    foreach (var item in writes)
                    {
                        await WriteSingleRegister(comm, slaveId, item.addr, item.val);
                        await Task.Delay(20);
                    }
                }
                else
                {
                    int idx = 0;
                    while (idx < writes.Count)
                    {
                        int count = 1;
                        while (idx + count < writes.Count && writes[idx + count].addr == writes[idx].addr + count) count++;

                        ushort startAddr = writes[idx].addr;
                        ushort[] values = new ushort[count];
                        for (int k = 0; k < count; k++) values[k] = writes[idx + k].val;

                        if (count == 1) await WriteSingleRegister(comm, slaveId, startAddr, values[0]);
                        else await WriteMultipleRegisters(comm, slaveId, startAddr, values);

                        idx += count;
                    }
                }
                
                // 2. 握手触发 (Trigger)
                if (!string.IsNullOrEmpty(cfg.TriggerAddress) && ushort.TryParse(cfg.TriggerAddress, out ushort trigAddr))
                {
                    await WriteSingleRegister(comm, slaveId, trigAddr, (ushort)cfg.TriggerValue);
                    if (cfg.IsTriggerPulse)
                    {
                        await Task.Delay(500);
                        await WriteSingleRegister(comm, slaveId, trigAddr, 0);
                    }
                }
        }

        private async Task PerformWriteRuntime(Communicationlib.TaskEngine.CommRuntime runtime, byte slaveId, TimeSyncConfig cfg, DateTime now)
        {
            // Runtime 封装了地址解析，直接传字符串即可
            // 但是 Runtime 不一定支持 SlaveId 的动态指定 (通常在 Protocol 内部写死或者在地址字符串里如 "1;40001")
            // 如果协议支持 "s=1;40001" 这种格式最好。S7 和 Modbus 实现通常有差异。
            // 假设直接写地址。
            
            // 定义一个本地帮助方法
            Func<string, int, Task> writeOne = async (addr, val) => 
            {
               if(string.IsNullOrWhiteSpace(addr)) return;
               await runtime.WriteAsync(addr, (ushort)val);
            };

            await writeOne(cfg.AddressYear, now.Year);
            await writeOne(cfg.AddressMonth, now.Month);
            await writeOne(cfg.AddressDay, now.Day);
            await writeOne(cfg.AddressHour, now.Hour);
            await writeOne(cfg.AddressMinute, now.Minute);
            
             // 处理秒/毫秒
            int secValue = now.Second;
            if (cfg.SecondFormat == 1) secValue = now.Second * 1000 + now.Millisecond;
            await writeOne(cfg.AddressSecond, secValue);
            
             // 2. 握手触发
            if (!string.IsNullOrEmpty(cfg.TriggerAddress))
            {
                 await writeOne(cfg.TriggerAddress, cfg.TriggerValue);
                 if (cfg.IsTriggerPulse)
                 {
                     await Task.Delay(500);
                     await writeOne(cfg.TriggerAddress, 0);
                 }
            }
        }

        private void AddWriteTask(List<(ushort, ushort)> list, string addrStr, int val)
        {
            if (!string.IsNullOrWhiteSpace(addrStr) && ushort.TryParse(addrStr, out ushort addr))
            {
                list.Add((addr, (ushort)val));
            }
        }

        #region Modbus Low Level Helpers

        // 这里我们手动构建 Modbus RTU 报文，通过 ICommBase.SendAndReceive<byte[]> 发送
        // 这样可以灵活控制 SlaveID，无需依赖此时 Client 的默认 ID。

        private async Task WriteSingleRegister(ICommBase comm, byte slaveId, ushort address, ushort value)
        {
            // FC06: [Slave] [06] [AddrHi] [AddrLo] [ValHi] [ValLo] [CRC] [CRC]
            byte[] frame = new byte[6];
            frame[0] = slaveId;
            frame[1] = 0x06;
            frame[2] = (byte)(address >> 8);
            frame[3] = (byte)(address & 0xFF);
            frame[4] = (byte)(value >> 8);
            frame[5] = (byte)(value & 0xFF);
            
            // CRC 由底层添加还是这里添加？
            // 通讯库通常 Send(byte[]) 会自动加 CRC 吗？
            // 看 ModbusClientBase 的 SendRaw 是纯虚方法。
            // 假设 ICommBase.SendAndReceive 接收的是 PDU 还是完整 ADU？
            // 绝大多数通用库 SendRaw 是发送已经打包好的完整帧 (ADU)。
            // 我们需要计算 CRC。
            
            byte[] adu = AppendCRC(frame);
            
            await Task.Run(() => comm.SendAndReceive(adu));
        }

        private async Task WriteMultipleRegisters(ICommBase comm, byte slaveId, ushort startAddress, ushort[] values)
        {
            // FC16
            int byteCount = values.Length * 2;
            byte[] frame = new byte[7 + byteCount];
            frame[0] = slaveId;
            frame[1] = 0x10;
            frame[2] = (byte)(startAddress >> 8);
            frame[3] = (byte)(startAddress & 0xFF);
            frame[4] = (byte)(values.Length >> 8);
            frame[5] = (byte)(values.Length & 0xFF);
            frame[6] = (byte)byteCount;

            for (int i = 0; i < values.Length; i++)
            {
                frame[7 + i * 2] = (byte)(values[i] >> 8);
                frame[8 + i * 2] = (byte)(values[i] & 0xFF);
            }

            byte[] adu = AppendCRC(frame);
            await Task.Run(() => comm.SendAndReceive(adu));
        }

        private byte[] AppendCRC(byte[] data)
        {
            byte[] res = new byte[data.Length + 2];
            Array.Copy(data, res, data.Length);
            
            ushort crc = 0xFFFF;
            for (int pos = 0; pos < data.Length; pos++)
            {
                crc ^= (ushort)data[pos];
                for (int i = 8; i != 0; i--)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                        crc >>= 1;
                }
            }
            
            res[res.Length - 2] = (byte)(crc & 0xFF);
            res[res.Length - 1] = (byte)(crc >> 8);
            return res;
        }

        #endregion

        private int GetDeviceSlaveId(ConfigEntity cfg)
        {
            // 尝试从参数里找 SlaveId
            if (cfg.CommParsams != null)
            {
                var p = cfg.CommParsams.FirstOrDefault(x => x.Name.Contains("Slave") || x.Name.Contains("Station") || x.Name.Contains("站号"));
                if (p != null && int.TryParse(p.Value, out int id)) return id;
            }
            return 1; // Default
        }

        private List<int> ParseSlaveIdRange(string rangeStr)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(rangeStr)) return list;

            try
            {
                var parts = rangeStr.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (part.Contains('-'))
                    {
                        var range = part.Split('-');
                        if (range.Length == 2 && int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                        {
                            for (int i = start; i <= end; i++) list.Add(i);
                        }
                    }
                    else
                    {
                        if (int.TryParse(part, out int id)) list.Add(id);
                    }
                }
            }
            catch { }
            return list;
        }
    }
}
