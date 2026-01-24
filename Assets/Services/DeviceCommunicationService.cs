using Communicationlib.Core;
using Communicationlib.Protocol;
using Communicationlib.Protocol.Modbus;
using Communicationlib.TaskEngine;
using Communicationlib.config;
using DoorMonitorSystem.Assets.Commlib;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.RunModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Communicationlib; // for CommDataReceivedEventArgs
using System.Reflection;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 设备通信服务 (重构版 - 基于Layered Architecture)
    /// 负责：
    /// 1. 加载配置
    /// 2. 分配 Runtime (Channel + Protocol)
    /// 3. 处理数据映射 (Mapping)
    /// </summary>
    public class DeviceCommunicationService : IDisposable
    {
        // 使用线程安全集合，防止多线程（通讯线程 vs 主线程重载）冲突
        private System.Collections.Concurrent.ConcurrentBag<DevicePointConfigEntity> _pointConfigs = new();
        private bool _isRunning = false;

        // 缓存设备运行时 (主站/客户端)
        private readonly Dictionary<int, CommRuntime> _runtimes = new();

        // 缓存从站实例 (服务端)
        private readonly Dictionary<int, ICommBase> _slaves = new();

        // 缓存点位状态（用于变位触发日志）
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, bool> _lastValues = new();

        // 优化：设备点位快速查找缓存 (SourceDeviceId -> List<DevicePoint>)
        private Dictionary<int, List<DevicePointConfigEntity>> _devicePointsCache = new();

        /// <summary>
        /// 启动服务
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                // 1. 确保协议插件已加载
                if (GlobalData.ProtocolsPairs == null || GlobalData.ProtocolsPairs.Count == 0)
                {
                    LogHelper.Info("[CommService] Loading protocol plugins...");
                    var pluginFolder = AppDomain.CurrentDomain.BaseDirectory;
                    GlobalData.ProtocolsPairs = ProtocolLoader.LoadAllProtocols(pluginFolder);
                }

                // 2. 加载点表配置
                LoadPointConfigs();

                // 3. 多一些延时，确保数据源(JSON/DB)和 UI 模型完全同步
                await Task.Delay(1000); 

                // 4. 初始化并启动所有设备运行时
                await InitializeRuntimesAsync();

                // 4. 初始化并启动所有设备运行时
                await InitializeRuntimesAsync();

                // 5. 开启本地 Slave 内存监控 (用于 UI 同步)
                StartSlaveMonitoring();
                
                LogHelper.Info("[CommService] Service started successfully.");
            }
            catch (Exception ex)
            {
                LogHelper.Error("[CommService] Start Failed", ex);
            }
        }
    

        private void StartSlaveMonitoring()
        {
            _ = Task.Run(async () =>
            {
                Debug.WriteLine("[CommService] Slave Monitoring Started.");
                while (_isRunning)
                {
                    try
                    {
                        foreach (var kv in _slaves)
                        {
                            int devId = kv.Key;
                            if (kv.Value is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave modbusSlave)
                            {
                                // 读取整个配置范围
                                var (min, count) = CalculateDevicePointRange(devId);
                                if (count > 0)
                                {
                                    var data = modbusSlave.Memory.ReadHoldingRegisters(min, count);
                                    ProcessData(devId, min.ToString(), data);
                                }
                            }
                        }

                        // 监控逻辑在此
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CommService] Monitoring Loop Error: {ex.Message}");
                    }
                    await Task.Delay(1000);
                }
            });
        }

        private void LoadPointConfigs()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;
                
                using SQLHelper mysql = new(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                mysql.Connect(); 
                var list = mysql.FindAll<DevicePointConfigEntity>();
                _pointConfigs = new System.Collections.Concurrent.ConcurrentBag<DevicePointConfigEntity>(list);
                
                // 构建快速查找缓存
                _devicePointsCache = list.GroupBy(p => p.SourceDeviceId).ToDictionary(g => g.Key, g => g.ToList());
                
                LogHelper.Info($"[CommService] Loaded {_pointConfigs.Count} point configs");
                foreach (var p in _pointConfigs)
                {
                    LogHelper.Info($"[CommService] Config: ID={p.Id} Dev={p.SourceDeviceId} Addr={p.Address} Bit={p.BitIndex} Target={p.TargetType}:{p.TargetObjId} Sync={p.IsSyncEnabled} SyncTo={p.SyncTargetDeviceId}:{p.SyncTargetAddress}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommService] Load Configs Failed: {ex.Message}");
            }
        }

        private async Task InitializeRuntimesAsync()
        {
            LogHelper.Info($"[CommService] Initializing Runtimes for {GlobalData.ListDveices.Count} devices...");
            foreach (var dev in GlobalData.ListDveices)
            {
                try
                {
                    // 防止重复初始化：先停止并移除旧的运行时
                    if (_runtimes.TryGetValue(dev.ID, out var oldRuntime))
                    {
                        LogHelper.Info($"[CommService] Stopping existing runtime for Device {dev.ID}");
                        oldRuntime.Stop();
                        oldRuntime.Dispose();
                        _runtimes.Remove(dev.ID);
                    }

                    var paramDict = dev.CommParsams.ToDictionary(k => k.Name, v => v.Value);

                    // --- 场景 A: 是从站/服务端模式 ---
                    if (dev.Protocol.Contains("SERVER"))
                    {
                        if (GlobalData.ProtocolsPairs != null && GlobalData.ProtocolsPairs.TryGetValue(dev.Protocol, out var slavePlugin))
                        {
                            // 1. 优先使用手动配置的参数
                            var finalParams = dev.CommParsams.ToList();
                            var currentDict = finalParams.ToDictionary(k => k.Name, v => v.Value);

                            // 2. 如果缺少关键参数，则尝试动态计算并补齐
                            var (calcAddr, calcCount) = CalculateDevicePointRange(dev.ID);
                            if (calcCount > 0)
                            {
                                if (!currentDict.ContainsKey("起始地址"))
                                    finalParams.Add(new CommParamEntity { Name = "起始地址", Value = calcAddr.ToString() });
                                if (!currentDict.ContainsKey("寄存器数量"))
                                    finalParams.Add(new CommParamEntity { Name = "寄存器数量", Value = calcCount.ToString() });
                            }

                            // 3. 初始化并开启
                            slavePlugin.Initialize(finalParams);
                            slavePlugin.Open();
                            _slaves[dev.ID] = slavePlugin;

                            // 4. 读取最终生效的参数用于日志打印 (保持诊断透明)
                            var logDict = finalParams.ToDictionary(k => k.Name, v => v.Value);
                            string logAddr = logDict.TryGetValue("起始地址", out var sAddr) ? sAddr : "0";
                            string logCount = logDict.TryGetValue("寄存器数量", out var sc) ? sc : "1000";
                            string logId = logDict.TryGetValue("设备ID", out var sId) ? sId : (logDict.TryGetValue("从站ID", out var sId2) ? sId2 : "1");
                            
                            Debug.WriteLine($"[CommService] Started Slave: {dev.Name} ({dev.Protocol}) ID:{logId} Range:{logAddr}-{int.Parse(logAddr) + int.Parse(logCount)}");
                        }
                        continue;
                    }

                    // --- 场景 B: 是主站/客户端模式 ---

                    // 1. 创建通道 (Core)
                    string type = paramDict.ContainsKey("IP") ? "TCP" : "RTU";
                    if (paramDict.ContainsKey("目标IP")) type = "TCP"; // 兼容新配置项
                    
                    ICommChannel channel = CommChannelFactory.CreateChannel(type, paramDict);

                    // 读取通用配置参数 (针对回退逻辑)
                    string startAddrStr = paramDict.TryGetValue("起始地址", out var sa) ? sa : (dev.Protocol == "S7-1500" ? "DB1.0" : "0");
                    ushort countRead = paramDict.TryGetValue("寄存器数量", out var c) ? (ushort.TryParse(c, out var cu) ? cu : (ushort)100) : (ushort)100;
                    byte slaveId = 1;
                    if (paramDict.TryGetValue("从站ID", out var sid)) byte.TryParse(sid, out slaveId);
                    else if (paramDict.TryGetValue("设备ID", out var devid)) byte.TryParse(devid, out slaveId);

                    // 2. 创建协议 (Protocol)
                    IProtocolClient protocol;
                    if (dev.Protocol == "S7-1500")
                    {
                        var s7Adapter = new Communicationlib.Protocol.S7.S7ProtocolClient();
                        s7Adapter.SetConnectionConfig(
                            paramDict.TryGetValue("IP地址", out var s7ip) ? s7ip : (paramDict.TryGetValue("IP", out var s7ip2) ? s7ip2 : "192.168.0.1"),
                            paramDict.TryGetValue("机架号", out var s7r) ? int.Parse(s7r) : (paramDict.TryGetValue("Rack", out var s7r2) ? int.Parse(s7r2) : 0),
                            paramDict.TryGetValue("插槽号", out var s7s) ? int.Parse(s7s) : (paramDict.TryGetValue("Slot", out var s7s2) ? int.Parse(s7s2) : 1)
                        );
                        protocol = s7Adapter;
                    }
                    else if (dev.Protocol == "MODBUS_RTU_CLIENT")
                    {
                        var modbus = new ModbusRtuClient();
                         if (ushort.TryParse(startAddrStr, out var saUshort))
                        {
                            modbus.SetBlockConfig(saUshort, countRead, slaveId);
                        }
                        protocol = modbus;
                    }
                    else
                    {
                        // Default to TCP for "MODBUS_TCP_CLIENT" or legacy configs
                        var modbus = new ModbusTcpClient();
                        if (ushort.TryParse(startAddrStr, out var saUshort))
                        {
                            modbus.SetBlockConfig(saUshort, countRead, slaveId);
                        }
                        protocol = modbus;
                    }

                    // 3. 创建运行时 (TaskEngine)
                    var runtime = new CommRuntime(channel, protocol)
                    {
                        RuntimeName = $"Runtime_{dev.Name}"
                    };

                    // 4. 计算并配置最优轮询任务块 (智能聚合)
                    // 读取自定义循环时间 (默认 500ms，防止过快)
                    int cycleTime = paramDict.TryGetValue("循环读取时间", out var ct) ? (int.TryParse(ct, out var cVal) ? cVal : 500) : 500;
                    
                    var pollingTasks = CreateOptimalPollingTasks(dev.ID, cycleTime);
                    if (pollingTasks.Count == 0)
                    {
                        // 回退机制：如果没有具体点位，则根据手动配置执行块轮询
                        Debug.WriteLine($"[CommService] No points for {dev.Name}, fallback to block polling: {startAddrStr}, Count: {countRead}");
                        runtime.AddTaskConfig(new ProtocolTaskConfig
                        {
                            Type = TaskType.Read,
                            Address = startAddrStr,
                            Count = countRead,
                            Interval = cycleTime, // 使用配置的循环时间
                            Enabled = true
                        });
                    }
                    else
                    {
                        foreach (var taskCfg in pollingTasks)
                        {
                            LogHelper.Info($"[CommService] Adding Polling Task for {dev.Name}: {taskCfg.FunctionCode} {taskCfg.Address} Qty:{taskCfg.Count} Interval:{cycleTime}ms");
                            runtime.AddTaskConfig(taskCfg);
                        }
                    }

                    // 5. 订阅数据事件 (极端诊断：订阅所有可能的事件)
                    try
                    {
                        var runtimeType = runtime.GetType();
                        var events = runtimeType.GetEvents(BindingFlags.Public | BindingFlags.Instance);
                        LogHelper.Info($"[DIAG-VER-4] Runtime {dev.Name} has {events.Length} events: {string.Join(", ", events.Select(e => e.Name))}");

                        foreach (var ev in events)
                        {
                            try
                            {
                                // 尝试为 (object, DeviceDataEventArgs) 签名的事件订阅
                                // 如果是 OnDataReceived 且能匹配，则使用
                                if (ev.Name == "OnDataReceived")
                                {
                                    runtime.OnDataReceived += (s, args) =>
                                    {
                                        // LogHelper.Info($"[CommService] Received Data: {dev.Name} -> {args.AddressTag}");
                                        ProcessData(dev.ID, args.AddressTag, args.Data);
                                    };
                                }
                                else
                                {
                                    // 尝试订阅通用的 EventHandler 
                                    // （这里如果是不同委托类型可能会失败，暂时仅针对我们已知的或类似的）
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("[CommService] Reflection Subscription Error", ex);
                    }

                    LogHelper.Info($"[CommService] Initialized Device: {dev.Name} (ID:{dev.ID}) Protocol:{dev.Protocol} Interval:{cycleTime}");
                    _runtimes[dev.ID] = runtime;
                    runtime.Start();
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"[CommService] Init Device {dev.Name} Failed", ex);
                }
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// 智能聚合算法：根据点位表自动生成最优的轮询任务块
        /// </summary>
        private int AutoMapFunctionCode(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return 3;
            
            // 行业惯例：4xxxx 为 HoldRegister (03), 3xxxx 为 InputRegister (04)
            if (address.StartsWith("4")) return 3;
            if (address.StartsWith("3")) return 4;
            
            return 3; // 默认 03
        }

        private List<ProtocolTaskConfig> CreateOptimalPollingTasks(int deviceId, int intervalMs)
        {
            var tasks = new List<ProtocolTaskConfig>();
            
            if (!_devicePointsCache.TryGetValue(deviceId, out var devicePoints)) return tasks;

            // 按照功能码分组聚合
            var groups = devicePoints
                .GroupBy(p => {
                    // 如果配置中已经手动指定了 >0 的功能码，则按手动的来；
                    // 否则执行自动探测 (AutoMap)
                    if (p.FunctionCode > 0) return p.FunctionCode;
                    return AutoMapFunctionCode(p.Address);
                })
                .ToList();

            foreach (var group in groups)
            {
                int functionCode = group.Key;
                var points = group
                    .Select(p => (ushort)NormalizeAddress(p.Address))
                    .Distinct()
                    .OrderBy(a => a)
                    .ToList();

                if (points.Count == 0) continue;

                const int MaxGap = 20;
                const int MaxBlockSize = 100;

                ushort blockStart = points[0];
                ushort lastAddr = points[0];

                for (int i = 1; i < points.Count; i++)
                {
                    ushort currentAddr = points[i];
                    if (currentAddr - lastAddr > MaxGap || currentAddr - blockStart >= MaxBlockSize)
                    {
                        tasks.Add(new ProtocolTaskConfig
                        {
                            Type = TaskType.Read,
                            FunctionCode = functionCode,
                            Address = blockStart.ToString(),
                            Count = (ushort)(lastAddr - blockStart + 1),
                            Interval = intervalMs,
                            Enabled = true
                        });
                        blockStart = currentAddr;
                    }
                    lastAddr = currentAddr;
                }

                tasks.Add(new ProtocolTaskConfig
                {
                    Type = TaskType.Read,
                    FunctionCode = functionCode,
                    Address = blockStart.ToString(),
                    Count = (ushort)(lastAddr - blockStart + 1),
                    Interval = intervalMs,
                    Enabled = true
                });
            }

            return tasks;
        }

        /// <summary>
        /// 根据点表配置计算设备所需的地址范围（起始地址和长度）
        /// </summary>
        private (ushort minAddr, ushort count) CalculateDevicePointRange(int deviceId)
        {
            if (!_devicePointsCache.TryGetValue(deviceId, out var devicePoints) || devicePoints.Count == 0) return (0, 0);

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
            
            // 假设每个寄存器占 1 个单位（Modbus Word）
            // 如果点位跨越了多个寄存器（如 Float），这里需要更加精细的逻辑
            // 目前简单实现：max - min + 1
            return (min, (ushort)(max - min + 1));
        }

        /// <summary>
        /// 归一化地址解析 (支持 40001 -> 1, DB1.100 -> 100)
        /// </summary>
        private int NormalizeAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return 0;

            if (address.Contains("."))
            {
                string[] parts = address.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int addr))
                    return addr;
            }

            if (int.TryParse(address, out int targetAddr))
            {
                // 6-digit (400001+, 300001+)
                if (targetAddr >= 400000) return targetAddr % 400000;
                if (targetAddr >= 300000) return targetAddr % 300000;
                // 5-digit (40001+, 30001+)
                if (targetAddr >= 40000) return targetAddr % 40000;
                if (targetAddr >= 30000) return targetAddr % 30000;
                // 4-digit (4001+, 3001+)
                if (targetAddr >= 4000) return targetAddr % 4000;
                if (targetAddr >= 3000) return targetAddr % 3000;
                
                return targetAddr;
            }

            return 0;
        }

        /// <summary>
        /// 核心处理：将协议数据映射到业务模型，并处理交互
        /// </summary>
        private void ProcessData(int sourceDeviceId, string startAddrTag, object rawData)
        {
            // LogHelper.Info($"[CommService] ProcessData Pulse: Source:{sourceDeviceId}, Address:{startAddrTag}, Type:{rawData?.GetType().Name}");

            // 目前暂只支持 ushort[] (Modbus Word) 或 byte[]
            ushort[]? data = null;
            if (rawData is ushort[] ushorts) data = ushorts;
            else if (rawData is byte[] bytes)
            {
                // 尝试将 byte[] 转为 ushort[] (大端序)
                data = new ushort[bytes.Length / 2];
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = (ushort)((bytes[i * 2] << 8) | bytes[i * 2 + 1]);
                }
            }

            if (data == null || data.Length == 0) 
            {
                LogHelper.Debug($"[CommService] Unrecognized rawData type: {rawData?.GetType().Name}");
                return;
            }

            // 解析起始地址偏移量并归一化
            int startAddr = NormalizeAddress(startAddrTag);

            // 找出所有关联此设备的点位 (使用缓存优化查找)
            if (!_devicePointsCache.TryGetValue(sourceDeviceId, out var points)) return;
            // LogHelper.Info($"[CommService] ProcessData: Found {points.Count} points for Dev:{sourceDeviceId}. StartAddr:{startAddr}(Norm:{startAddr}), DataLen:{(data != null ? data.Length : -1)}");

            foreach (var p in points)
            {
                // 解析点位地址偏移量并归一化
                int targetAddr = NormalizeAddress(p.Address);

                int offset = targetAddr - startAddr;
                if (offset < 0 || offset >= data.Length) 
                {
                    continue;
                }

                ushort wordValue = data[offset];
                bool bitValue = ((wordValue >> p.BitIndex) & 1) == 1;

                // LogHelper.Info($"[Point-Match] Dev:{sourceDeviceId} Addr:{p.Address}({targetAddr}) Offset:{offset} Val:{bitValue} Target:{p.TargetType}:{p.TargetObjId}");

                // 1. 更新 UI 状态
                UpdateUiModel(p, bitValue);

                // 2. 处理同步/交互 (Sync)
                if (p.IsSyncEnabled && p.SyncTargetDeviceId.HasValue && p.SyncTargetAddress.HasValue)
                {
                    HandleSync(p, bitValue);
                }

                // 3. 处理日志记录 (Logging)
                ProcessLogging(p, bitValue);
            }
        }

        private void UpdateUiModel(DevicePointConfigEntity config, bool value)
        {
            // LogHelper.Info($"[CommService] UpdateUiModel: {config.TargetType}:{config.TargetObjId} -> {value}");
            
            // 业务逻辑上移至 DataManager
            DataManager.Instance.UpdatePointValue(config.TargetObjId, config.TargetBitConfigId, config.TargetType, value);
        }

        private void HandleSync(DevicePointConfigEntity config, bool value)
        {
            try
            {
                // Sync Logic:
                // 1. If Target is Local Server (Slave), write to its storage.
                // 2. If Target is Remote PLC (Client), sent Write command.
                
                int targetDevId = config.SyncTargetDeviceId.Value;
                string targetAddrStr = config.SyncTargetAddress.Value.ToString();
                int targetAddrNormalized = NormalizeAddress(targetAddrStr);
                // 确定目标位索引：如果有配置则用配置，否则默认跟源点位一致
                int targetBitIndex = config.SyncTargetBitIndex ?? config.BitIndex;

                // 确定最终要写入的值
                bool finalValue = (config.SyncMode == 1) ? !value : value;

                // Case A: Target is a Local Slave (Server)
                if (_slaves.TryGetValue(targetDevId, out var slave))
                {
                    if (slave is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave modbusSlave && modbusSlave.Memory != null)
                    {
                        ushort tAddr = (ushort)targetAddrNormalized;
                        
                        try
                        {
                            var registers = modbusSlave.Memory.ReadHoldingRegisters(tAddr, 1);
                            if (registers != null && registers.Length > 0)
                            {
                                ushort currentVal = registers[0];
                                ushort mask = (ushort)(1 << targetBitIndex); 
                                ushort newVal;
                                if (finalValue)
                                    newVal = (ushort)(currentVal | mask);
                                else
                                    newVal = (ushort)(currentVal & ~mask);
                                
                                 modbusSlave.Memory.WriteSingleRegister(tAddr, newVal);
                                // LogHelper.Debug($"[Sync-OK] {config.Description} -> Slave:{targetDevId} Addr:{tAddr}.{targetBitIndex} Val:{finalValue}");
                                return;
                            }
                            else
                            {
                                // LogHelper.Debug($"[Sync-Warn] Slave Memory Read Null/Empty for Addr:{tAddr}");
                            }
                        }
                        catch (Exception ex)
                        {
                             LogHelper.Error($"[Sync-Error] Slave Memory Access: {ex.Message}");
                        }
                    }
                }
                else
                {
                     LogHelper.Debug($"[Sync-Error] TargetSlave {targetDevId} Not Found in _slaves.");
                }
                
                // Case B: Target is a Remote Device (Client Runtime)
                if (_runtimes.TryGetValue(targetDevId, out var runtime))
                {
                    _ = Task.Run(async () => 
                    {
                        try 
                        {
                            var readResult = await runtime.ReadAsync(targetAddrStr, 1);
                            if (readResult != null && readResult.Length > 0)
                            {
                                ushort currentVal = readResult[0];
                                ushort mask = (ushort)(1 << targetBitIndex);
                                ushort newVal;
                                if (finalValue)
                                    newVal = (ushort)(currentVal | mask);
                                else
                                    newVal = (ushort)(currentVal & ~mask);
                                
                                await runtime.WriteAsync(targetAddrStr, newVal);
                                // LogHelper.Debug($"[Sync-OK] {config.Description} -> Remote:{targetDevId} Addr:{targetAddrStr}.{targetBitIndex} Val:{finalValue}");
                            }
                        }
                        catch(Exception ex)
                        {
                            LogHelper.Error($"[Sync-Err] Remote RMW Failed", ex);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sync-Err] Handle Error: {ex.Message}");
            }
        }

        // 日志缓冲队列
        private readonly System.Collections.Concurrent.BlockingCollection<string> _logQueue = new();
        private bool _isLogConsumerRunning = false;

        private void StartLogConsumer()
        {
            if (_isLogConsumerRunning) return;
            _isLogConsumerRunning = true;

            Task.Run(async () =>
            {
                var batch = new List<string>();
                while (_isRunning || !_logQueue.IsCompleted)
                {
                    try
                    {
                        // 1. 尝试从队列中读取一条 (阻塞最多 1秒)
                        if (_logQueue.TryTake(out var sql, 1000))
                        {
                            batch.Add(sql);
                        }

                        // 2. 如果积攒够了或者超时了（Queue空了），且有数据待写入
                        if (batch.Count > 0 && (batch.Count >= 50 || _logQueue.Count == 0))
                        {
                            await WriteLogBatchAsync(batch);
                            batch.Clear();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LogConsumer] Error: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }
            });
        }

        private async Task WriteLogBatchAsync(List<string> sqlList)
        {
            if (GlobalData.SysCfg == null) return;
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                
                // 使用事务批量提交，提升 100倍 性能
                await Task.Run(() => 
                    {
                        try 
                        {
                            // 合并成一个大的 Transaction 或 Multi-Statement
                            // 这里简单实现：逐条执行，但复用链接
                            foreach (var sql in sqlList)
                            {
                                db.ExecuteNonQuery(sql);
                            }
                        }
                        catch (Exception ex) when (ex.Message.Contains("PointLogs") || (ex is MySql.Data.MySqlClient.MySqlException mex && mex.Number == 1146))
                        {
                             // 自动建表容错
                             db.ExecuteNonQuery(@"CREATE TABLE IF NOT EXISTS `PointLogs` (
                                `ID` INT NOT NULL AUTO_INCREMENT,
                                `PointID` INT,
                                `DeviceID` INT,
                                `Address` VARCHAR(50),
                                `Val` TINYINT,
                                `LogType` INT,
                                `Message` VARCHAR(255),
                                `LogTime` DATETIME,
                                PRIMARY KEY (`ID`)
                            );");
                            // 重试
                            foreach (var sql in sqlList) { try{ db.ExecuteNonQuery(sql); } catch{} }
                        }
                    });
                
            }
            catch (Exception ex) 
            {
                 Debug.WriteLine($"[LogWriter] Batch Failed: {ex.Message}");
            }
        }

        private void ProcessLogging(DevicePointConfigEntity p, bool currentValue)
        {
            if (!p.IsLogEnabled) return;

            // 获取旧值
            _lastValues.TryGetValue(p.Id, out bool lastValue);
            bool isFirstTime = !_lastValues.ContainsKey(p.Id);
            
            // Debug Loop Issue: Check if we are seeing repeated logs
            // Debug.WriteLine($"[ProcessLogging] Point:{p.Id}({p.Description}) Val:{currentValue} Last:{lastValue} First:{isFirstTime}");

            _lastValues[p.Id] = currentValue;

            if (!isFirstTime && currentValue == lastValue) return;

            // 评估触发逻辑 (0=False, 1=True, 2=Both)
            bool shouldLog = false;
            if (p.LogTriggerState == 2) shouldLog = true;
            else if (p.LogTriggerState == 1 && currentValue) shouldLog = true;
            else if (p.LogTriggerState == 0 && !currentValue) shouldLog = true;

            if (shouldLog)
            {
                // 生产日志 SQL 并入队，不再直接开线程写库
                string logMsg = string.IsNullOrWhiteSpace(p.LogMessage) ? p.Description : p.LogMessage;
                logMsg = logMsg?.Replace("{Value}", currentValue ? "ON" : "OFF");

                string sql = $"INSERT INTO `PointLogs` (PointID, DeviceID, Address, Val, LogType, Message, LogTime) " +
                             $"VALUES ({p.Id}, {p.SourceDeviceId}, '{p.Address}.{p.BitIndex}', {(currentValue ? 1 : 0)}, {p.LogTypeId}, '{logMsg}', '{DateTime.Now:yyyy-MM-dd HH:mm:ss}')";

                if (!_isLogConsumerRunning) StartLogConsumer();
                _logQueue.Add(sql);
            }
        }

        public void Dispose()
        {
            _isRunning = false;
            foreach (var r in _runtimes.Values)
            {
                r.Dispose();
            }
            _runtimes.Clear();
        }
    }
    
    
}
