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
        private List<DevicePointConfigEntity> _pointConfigs = new();
        private bool _isRunning = false;

        // 缓存设备运行时 (主站/客户端)
        private readonly Dictionary<int, CommRuntime> _runtimes = new();

        // 缓存从站实例 (服务端)
        private readonly Dictionary<int, ICommBase> _slaves = new();

        // 缓存点位状态（用于变位触发日志）
        private readonly Dictionary<int, bool> _lastValues = new();

        /// <summary>
        /// 启动服务
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                // 1. 加载点表配置
                LoadPointConfigs();

                // 2. 初始化并启动所有设备运行时
                await InitializeRuntimesAsync();

                // 3. 开启本地 Slave 内存监控 (用于 UI 同步)
                StartSlaveMonitoring();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommService] Start Failed: {ex.Message}");
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
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CommService] Slave Monitor Loop Error: {ex.Message}");
                    }
                    await Task.Delay(1000); // 1秒更新一次本地 Slave 的 UI
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
                _pointConfigs = mysql.FindAll<DevicePointConfigEntity>();
                Debug.WriteLine($"[CommService] Loaded {_pointConfigs.Count} point configs");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommService] Load Configs Failed: {ex.Message}");
            }
        }

        private async Task InitializeRuntimesAsync()
        {
            foreach (var dev in GlobalData.ListDveices)
            {
                try
                {
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
                    var pollingTasks = CreateOptimalPollingTasks(dev.ID);
                    if (pollingTasks.Count == 0)
                    {
                        // 回退机制：如果没有具体点位，则根据手动配置执行块轮询
                        Debug.WriteLine($"[CommService] No points for {dev.Name}, fallback to block polling: {startAddrStr}, Count: {countRead}");
                        runtime.AddTaskConfig(new ProtocolTaskConfig
                        {
                            Type = TaskType.Read,
                            Address = startAddrStr,
                            Count = countRead,
                            Interval = 500,
                            Enabled = true
                        });
                    }
                    else
                    {
                        foreach (var taskCfg in pollingTasks)
                        {
                            runtime.AddTaskConfig(taskCfg);
                        }
                    }

                    // 5. 订阅数据事件
                    runtime.OnDataReceived += (s, args) =>
                    {
                        ProcessData(dev.ID, args.AddressTag, args.Data);
                    };

                    _runtimes[dev.ID] = runtime;
                    runtime.Start();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CommService] Init Device {dev.Name} Failed: {ex.Message}");
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

        private List<ProtocolTaskConfig> CreateOptimalPollingTasks(int deviceId)
        {
            var tasks = new List<ProtocolTaskConfig>();
            
            // 按照功能码分组聚合
            var groups = _pointConfigs
                .Where(p => p.SourceDeviceId == deviceId)
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
                    .Select(p => { ushort.TryParse(p.Address, out ushort addr); return addr; })
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
                            Interval = 200,
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
                    Interval = 200,
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
            var devicePoints = _pointConfigs.Where(p => p.SourceDeviceId == deviceId).ToList();
            if (devicePoints.Count == 0) return (0, 0);

            ushort min = ushort.MaxValue;
            ushort max = ushort.MinValue;

            foreach (var p in devicePoints)
            {
                if (ushort.TryParse(p.Address, out ushort addr))
                {
                    if (addr < min) min = addr;
                    if (addr > max) max = addr;
                }
            }

            if (min == ushort.MaxValue) return (0, 0);
            
            // 假设每个寄存器占 1 个单位（Modbus Word）
            // 如果点位跨越了多个寄存器（如 Float），这里需要更加精细的逻辑
            // 目前简单实现：max - min + 1
            return (min, (ushort)(max - min + 1));
        }

        /// <summary>
        /// 核心处理：将协议数据映射到业务模型，并处理交互
        /// </summary>
        private void ProcessData(int sourceDeviceId, string startAddrTag, object rawData)
        {
            // Debug Log
            // Debug.WriteLine($"[CommService] Received Data: Source:{sourceDeviceId}, Address:{startAddrTag}");

            // 目前暂只支持 ushort[] (Modbus Word)
            ushort[]? data = rawData as ushort[];
            if (data == null || data.Length == 0) return;


            // 解析起始地址偏移量
            int startAddr = 0;
            if (startAddrTag.Contains("."))
            {
                // 处理 S7 格式 "DB1.100" -> 100
                string[] parts = startAddrTag.Split('.');
                if (parts.Length >= 2) int.TryParse(parts[1], out startAddr);
            }
            else
            {
                int.TryParse(startAddrTag, out startAddr);
            }

            // 找出所有关联此设备及由于地址范围重叠的点位
            var points = _pointConfigs.Where(p => p.SourceDeviceId == sourceDeviceId).ToList();

            foreach (var p in points)
            {
                // 解析点位地址偏移量并规格化 (支持 40001 -> 1)
                int targetAddr = 0;
                if (p.Address.Contains("."))
                {
                    string[] parts = p.Address.Split('.');
                    if (parts.Length >= 2) int.TryParse(parts[1], out targetAddr);
                }
                else
                {
                    if (int.TryParse(p.Address, out targetAddr))
                    {
                         if (targetAddr > 40000) targetAddr %= 40000;
                         else if (targetAddr > 30000) targetAddr %= 30000;
                    }
                }

                int offset = targetAddr - startAddr;
                if (offset < 0 || offset >= data.Length) continue;

                ushort wordValue = data[offset];
                bool bitValue = ((wordValue >> p.BitIndex) & 1) == 1;

                ModbusRawLogger.LogSync($"[Point-Match] Dev:{sourceDeviceId} Addr:{p.Address} Val:{bitValue} Sync:{p.IsSyncEnabled}");

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
            Application.Current.Dispatcher.Invoke(() =>
            {
                GlobalDataExtension.UpdatePointValue(config, value);
            });
        }

        private void HandleSync(DevicePointConfigEntity config, bool value)
        {
            try
            {
                // Sync Logic:
                // 1. If Target is Local Server (Slave), write to its storage.
                // 2. If Target is Remote PLC (Client), sent Write command.
                
                int targetDevId = config.SyncTargetDeviceId.Value;
                string targetAddr = config.SyncTargetAddress.Value.ToString();
                // 确定目标位索引：如果有配置则用配置，否则默认跟源点位一致
                int targetBitIndex = config.SyncTargetBitIndex ?? config.BitIndex;

                // 确定最终要写入的值
                bool finalValue = (config.SyncMode == 1) ? !value : value;

                // Case A: Target is a Local Slave (Server)
                if (_slaves.TryGetValue(targetDevId, out var slave))
                {
                    if (slave is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave modbusSlave && modbusSlave.Memory != null)
                    {
                        ushort tAddr = (ushort)config.SyncTargetAddress.Value;
                         // Normalize target address too
                         if (tAddr > 40000) tAddr = (ushort)(tAddr % 40000);
                         else if (tAddr > 30000) tAddr = (ushort)(tAddr % 30000);
                        
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
                                ModbusRawLogger.LogSync($"[Sync-OK] {config.Description} -> Slave:{targetDevId} Addr:{tAddr}.{targetBitIndex} Val:{finalValue}");
                                return;
                            }
                            else
                            {
                                ModbusRawLogger.LogSync($"[Sync-Warn] Slave Memory Read Null/Empty for Addr:{tAddr}");
                            }
                        }
                        catch (Exception ex)
                        {
                             ModbusRawLogger.LogSync($"[Sync-Error] Slave Memory Access: {ex.Message}");
                        }
                    }
                }
                else
                {
                     ModbusRawLogger.LogSync($"[Sync-Error] TargetSlave {targetDevId} Not Found in _slaves.");
                }
                
                // Case B: Target is a Remote Device (Client Runtime)
                if (_runtimes.TryGetValue(targetDevId, out var runtime))
                {
                    _ = Task.Run(async () => 
                    {
                        try 
                        {
                            var readResult = await runtime.ReadAsync(targetAddr, 1);
                            if (readResult != null && readResult.Length > 0)
                            {
                                ushort currentVal = readResult[0];
                                ushort mask = (ushort)(1 << targetBitIndex);
                                ushort newVal;
                                if (finalValue)
                                    newVal = (ushort)(currentVal | mask);
                                else
                                    newVal = (ushort)(currentVal & ~mask);
                                
                                await runtime.WriteAsync(targetAddr, newVal);
                                Debug.WriteLine($"[Sync-OK] Remote Client RMW: {targetDevId} Addr:{targetAddr}.{targetBitIndex} Val:{newVal} (BitVal:{finalValue})");
                            }
                        }
                        catch(Exception ex)
                        {
                            Debug.WriteLine($"[Sync-Err] RMW Failed: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sync-Err] Handle Error: {ex.Message}");
            }
        }

        private void ProcessLogging(DevicePointConfigEntity p, bool currentValue)
        {
            if (!p.IsLogEnabled) return;

            // 获取旧值
            _lastValues.TryGetValue(p.Id, out bool lastValue);
            bool isFirstTime = !_lastValues.ContainsKey(p.Id);
            _lastValues[p.Id] = currentValue;

            if (!isFirstTime && currentValue == lastValue) return;

            // 评估触发逻辑 (0=False, 1=True, 2=Both)
            bool shouldLog = false;
            if (p.LogTriggerState == 2) shouldLog = true;
            else if (p.LogTriggerState == 1 && currentValue) shouldLog = true;
            else if (p.LogTriggerState == 0 && !currentValue) shouldLog = true;

            if (shouldLog)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                        db.Connect();
                        
                        string logMsg = string.IsNullOrWhiteSpace(p.LogMessage) ? p.Description : p.LogMessage;
                        logMsg = logMsg?.Replace("{Value}", currentValue ? "ON" : "OFF");

                        string sql = $"INSERT INTO `PointLogs` (PointID, DeviceID, Address, Val, LogType, Message, LogTime) " +
                                     $"VALUES ({p.Id}, {p.SourceDeviceId}, '{p.Address}.{p.BitIndex}', {(currentValue ? 1 : 0)}, {p.LogTypeId}, '{logMsg}', '{DateTime.Now:yyyy-MM-dd HH:mm:ss}')";
                        
                        try { db.ExecuteNonQuery(sql); }
                        catch (Exception ex) when (ex.Message.Contains("PointLogs") || (ex is MySql.Data.MySqlClient.MySqlException mex && mex.Number == 1146))
                        {
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
                            db.ExecuteNonQuery(sql);
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"[Logging] Failed: {ex.Message}"); }
                });
            }
        }

        public void Dispose()
        {
            foreach (var r in _runtimes.Values)
            {
                r.Dispose();
            }
            _runtimes.Clear();
        }
    }
    
    // 扩展类，用于规避直接修改 GlobalData (保持原有逻辑不变)
    public static class GlobalDataExtension 
    {
        private static Dictionary<string, DoorBitConfig> _doorBitCache = new();
        private static Dictionary<string, PanelBitConfig> _panelBitCache = new();
        private static bool _isCacheBuilt = false;

        public static void UpdatePointValue(DevicePointConfigEntity config, bool value)
        {
            if (!_isCacheBuilt)
            {
                BuildCache();
            }

            string key = $"{config.TargetObjId}_{config.TargetBitConfigId}";

            if (config.TargetType == TargetType.Door)
            {
                if (_doorBitCache.TryGetValue(key, out var bitConfig))
                {
                    if (bitConfig.BitValue != value)
                    {
                        bitConfig.BitValue = value;
                    }
                }
            }
            else if (config.TargetType == TargetType.Panel)
            {
                if (_panelBitCache.TryGetValue(key, out var bitConfig))
                {
                    if (bitConfig.BitValue != value)
                    {
                        bitConfig.BitValue = value;
                    }
                }
            }
        }

        private static void BuildCache()
        {
            try
            {
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow?.DataContext is not DoorMonitorSystem.ViewModels.MainViewModel mainVm)
                    return;

                _doorBitCache.Clear();
                _panelBitCache.Clear();

                foreach (var stationVm in mainVm.Stations)
                {
                    if (stationVm.Station == null) continue;

                    foreach (var doorGroup in stationVm.Station.DoorGroups)
                    {
                        foreach (var door in doorGroup.Doors)
                        {
                            foreach (var bit in door.Bits)
                            {
                                string key = $"{door.DoorId}_{bit.BitId}"; 
                                _doorBitCache[key] = bit;
                            }
                        }
                    }

                    foreach (var panelGroup in stationVm.Station.PanelGroups)
                    {
                        foreach (var panel in panelGroup.Panels)
                        {
                            foreach (var bit in panel.BitList)
                            {
                                string key = $"{panel.PanelId}_{bit.BitId}";
                                _panelBitCache[key] = bit;
                            }
                        }
                    }
                }

                if (_doorBitCache.Count > 0 || _panelBitCache.Count > 0)
                {
                    _isCacheBuilt = true;
                    Debug.WriteLine($"[CommService] Cache built. Doors bits: {_doorBitCache.Count}, Panel bits: {_panelBitCache.Count}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CommService] Build Cache Failed: {ex.Message}");
            }
        }
    }
}
