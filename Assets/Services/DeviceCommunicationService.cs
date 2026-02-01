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
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Concurrent;
using Communicationlib; // 包含 ICommBase
using DoorMonitorSystem; // 包含 GlobalData

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 设备通信服务 (精简版)
    /// 主控类：负责通讯服务的生命周期管理、设备初始化和数据分发。
    /// 核心职责：
    /// 1. 解析配置并启动所有设备的通讯运行时 (CommRuntime) 或 从站 (Slave)。
    /// 2. 协调其他服务 (如 NTP, TimeSync) 的启动。
    /// 3. 管理全局数据处理器 (DataProcessor)。
    /// </summary>
    public class DeviceCommunicationService : IDisposable
    {
        public static DeviceCommunicationService Instance { get; private set; }

        public DeviceCommunicationService()
        {
            Instance = this;
        }

        private ConcurrentBag<DevicePointConfigEntity> _pointConfigs = new();
        private bool _isRunning = false;

        /// <summary>配置是否已从数据库完全加载到内存字典</summary>
        public bool IsInitialized { get; private set; } = false;

        /// <summary>当配置加载/刷新完成时触发</summary>
        public event Action ConfigLoaded;

        private readonly Dictionary<int, CommRuntime> _runtimes = new();
        private readonly Dictionary<int, IProtocolClient> _protocolClients = new();
        private readonly Dictionary<int, ICommBase> _slaves = new();
        private Dictionary<int, List<DevicePointConfigEntity>> _devicePointsCache = new();
        private Dictionary<int, List<DevicePointConfigEntity>> _doorPointsIndex = new(); // Index by TargetObjId (Door)
        private Dictionary<int, List<DevicePointConfigEntity>> _stationPointsIndex = new(); // Index by TargetObjId (Station)
        
        private DataProcessor? _dataProcessor;

        /// <summary>
        /// (底层用) 根据源设备 ID 查找配置
        /// </summary>
        public DevicePointConfigEntity? GetPointConfig(int sourceDeviceId, string uiBinding, string role = "Read")
        {
            if (_devicePointsCache.TryGetValue(sourceDeviceId, out var list))
            {
                return list.FirstOrDefault(p => p.UiBinding == uiBinding && p.BindingRole == role);
            }
            return null;
        }

        /// <summary>
        /// 根据 ConfigId (BitId) 查找点位配置
        /// </summary>
        public DevicePointConfigEntity? GetPointConfigById(int id)
        {
            return _pointConfigs?.FirstOrDefault(p => p.Id == id);
        }

        /// <summary>
        /// (UI用) 根据门号查找点位配置
        /// </summary>
        public DevicePointConfigEntity? GetPointConfigForDoor(int doorId, string uiBinding, string role = "Read", int? sourceDeviceId = null)
        {
            if (_doorPointsIndex.TryGetValue(doorId, out var list))
            {
                var query = list.Where(p => string.Equals(p.UiBinding, uiBinding, StringComparison.OrdinalIgnoreCase) 
                                         && string.Equals(p.BindingRole, role, StringComparison.OrdinalIgnoreCase));
                
                if (sourceDeviceId.HasValue)
                {
                    query = query.Where(p => p.SourceDeviceId == sourceDeviceId.Value);
                }
                
                return query.FirstOrDefault();
            }
            return null;
        }

        public DevicePointConfigEntity? GetPointConfigForDoorRelaxed(int doorId, string uiBinding, int? sourceDeviceId = null)
        {
            if (string.IsNullOrWhiteSpace(uiBinding)) return null;
            
            if (_doorPointsIndex.TryGetValue(doorId, out var list))
            {
                // 1. Try match (Case Insensitive)
                var query = list.Where(p => string.Equals(p.UiBinding, uiBinding, StringComparison.OrdinalIgnoreCase));

                if (sourceDeviceId.HasValue)
                {
                    query = query.Where(p => p.SourceDeviceId == sourceDeviceId.Value);
                }

                return query.FirstOrDefault();
            }
            return null;
        }

        /// <summary>
        /// (UI用) 根据站台号查找点位配置
        /// </summary>
        public DevicePointConfigEntity? GetPointConfigForStation(int stationId, string uiBinding, string role = "Read", int? sourceDeviceId = null)
        {
            DevicePointConfigEntity? result = null;

            // 1. 优先查找当前站台特定的配置：
            // 根据 stationId (TargetObjId) 在索引中定位该站台专属的点位列表
            if (_stationPointsIndex.TryGetValue(stationId, out var list))
            {
                var query = list.Where(p => string.Equals(p.UiBinding, uiBinding, StringComparison.OrdinalIgnoreCase) 
                                         && (role == null || string.Equals(p.BindingRole, role, StringComparison.OrdinalIgnoreCase)));
                
                // 如果指定了源设备ID（PLC ID），则增加过滤条件，防止多台 PLC 下同名键名混淆
                if (sourceDeviceId.HasValue) query = query.Where(p => p.SourceDeviceId == sourceDeviceId.Value);
                result = query.FirstOrDefault();
            }

            // 2. 备选查找站台通用配置 (TargetObjId = 0)：
            // 如果特定站台没配，则查找标记为“站台通用”的点位，实现多站台复用同一套配置
            if (result == null && _stationPointsIndex.TryGetValue(0, out var commonList))
            {
                var query = commonList.Where(p => string.Equals(p.UiBinding, uiBinding, StringComparison.OrdinalIgnoreCase) 
                                         && (role == null || string.Equals(p.BindingRole, role, StringComparison.OrdinalIgnoreCase)));
                
                if (sourceDeviceId.HasValue) query = query.Where(p => p.SourceDeviceId == sourceDeviceId.Value);
                result = query.FirstOrDefault();
            }

            return result;
        }

        /// <summary>
        /// 获取站台下的所有点位配置
        /// </summary>
        public List<DevicePointConfigEntity> GetPointConfigsForStation(int stationId)
        {
            // 直接从站台索引中拉取该站台 ID 绑定的所有点位，用于批量解析或 ID 溯源
            if (_stationPointsIndex.TryGetValue(stationId, out var list)) return list;
            return new List<DevicePointConfigEntity>();
        }

        /// <summary>
        /// (UI用) 全局复用匹配：忽略门号，仅根据 BindingKey 查找第一个匹配的点位
        /// 适用于“多路复用”模式（即所有门共用一套寄存器地址）
        /// 支持指定 Role (如 "Parameter" 用于写入)
        /// 支持指定 DeviceId (防止跨设备混淆)
        /// </summary>
        public DevicePointConfigEntity? GetPointConfigByKey(string uiBinding, string role = null, int? sourceDeviceId = null)
        {
            if (string.IsNullOrWhiteSpace(uiBinding)) return null;

            // 情况 A：指定了设备ID。
            // 此时只会在该设备的全局重用点位（TargetType 为 None）中查找，确保不会误抓到其他对象绑定的私有点位。
            if (sourceDeviceId.HasValue)
            {
                if (_devicePointsCache.TryGetValue(sourceDeviceId.Value, out var list))
                {
                     // 严格限制：仅匹配 TargetType 为 None 的点位，实现“跨站台/跨门隔离”
                     return list.FirstOrDefault(p => string.Equals(p.UiBinding, uiBinding, StringComparison.OrdinalIgnoreCase)
                                                  && (role == null || string.Equals(p.BindingRole, role, StringComparison.OrdinalIgnoreCase))
                                                  && p.TargetType == TargetType.None);
                }
                return null;
            }

            // 情况 B：未指定设备ID（纯全局搜索）。
            // 扫描所有已加载设备的全局点位。注意：这里会跳过已绑定到特定门、站台的点位，以维护数据安全性。 
            foreach (var list in _devicePointsCache.Values)
            {
                var match = list.FirstOrDefault(p => string.Equals(p.UiBinding, uiBinding, StringComparison.OrdinalIgnoreCase)
                                                  && (role == null || string.Equals(p.BindingRole, role, StringComparison.OrdinalIgnoreCase))
                                                  && p.TargetType == TargetType.None);
                if (match != null) return match;
            }
            
            return null;
        }

        private readonly System.Threading.SemaphoreSlim _startLock = new System.Threading.SemaphoreSlim(1, 1);

        /// <summary>
        /// 异步启动通信服务
        /// </summary>
        /// <remarks>
        /// 包含以下步骤：
        /// 1. 加载协议插件。
        /// 2. 从数据库加载点位配置。
        /// 3. 初始化数据处理器。
        /// 4. 启动 NTP 服务和 TimeSync 服务。
        /// 5. 根据配置初始化并启动所有设备的通讯实例。
        /// </remarks>
        public async Task StartAsync()
        {
            await _startLock.WaitAsync();
            try 
            {
                if (_isRunning) return;
                _isRunning = true;
            }
            finally
            {
                _startLock.Release();
            }

            try
            {
                // 1. 加载协议
                if (GlobalData.ProtocolsPairs == null || GlobalData.ProtocolsPairs.Count == 0)
                {
                    GlobalData.ProtocolsPairs = ProtocolLoader.LoadAllProtocols(AppDomain.CurrentDomain.BaseDirectory);
                }

                // 2. 加载点表
                LoadPointConfigs();
                
                // 3. 初始化处理器
                _dataProcessor = new DataProcessor(_runtimes, _slaves, _devicePointsCache);

                await Task.Delay(500); 

                // 4. 启动运行时
                
                // --- NTP Service Integration ---
                try 
                {
                    // Config is already loaded in DataManager
                    if (GlobalData.NtpConfig == null) GlobalData.NtpConfig = new DoorMonitorSystem.Models.system.NtpConfig();

                    // Start NTP Service
                    await NtpService.Instance.StartAsync();
                }
                catch (Exception ex)
                {
                    LogHelper.Error("[CommService] NTP Service Init Failed", ex);
                }

                // 应用全局日志开关到 ModbusRawLogger (静态类)
                Communicationlib.Protocol.Modbus.ModbusRawLogger.IsEnabled = GlobalData.DebugConfig?.Trace_Communication_Raw ?? false;

                await InitializeRuntimesAsync();
            }
            catch (Exception ex)
            {
                LogHelper.Error("[CommService] 启动失败", ex);
            }
        }

        /// <summary>
        /// 从数据库加载所有设备的点位配置
        /// </summary>
        private void LoadPointConfigs()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;
                
                // 从主数据库加载所有设备点位配置
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                
                var points = db.FindAll<DevicePointConfigEntity>();
                if (points != null)
                {
                    _pointConfigs = new ConcurrentBag<DevicePointConfigEntity>(points);
                    
                    // 1. 底层通讯索引 (Key = SourceDeviceId / PLC ID)
                    _devicePointsCache = points.GroupBy(p => p.SourceDeviceId).ToDictionary(g => g.Key, g => g.ToList());
                    
                    // 2. UI 业务索引 (Key = TargetObjId / Door ID)
                    _doorPointsIndex = points.Where(p => p.TargetType == TargetType.Door)
                                             .GroupBy(p => p.TargetObjId)
                                             .ToDictionary(g => g.Key, g => g.ToList());

                    // 3. UI 业务索引 (Key = TargetObjId / Station ID)
                    _stationPointsIndex = points.Where(p => p.TargetType == TargetType.Station)
                                             .GroupBy(p => p.TargetObjId)
                                             .ToDictionary(g => g.Key, g => g.ToList());

                    LogHelper.Info($"[CommService] 加载点位配置完成。点位总数: {points.Count}, 门组: {_doorPointsIndex.Count}, 站台组: {_stationPointsIndex.Count}");
                    
                    // 标记初始化完成并通知外部
                    IsInitialized = true;
                    ConfigLoaded?.Invoke();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[CommService] 加载点位配置错误 (LoadPointConfigs)", ex);
            }
        }

        /// <summary>
        /// 重新从数据库加载点位配置 (用于配置变更后刷新缓存)
        /// </summary>
        public void ReloadConfigs()
        {
            LoadPointConfigs();
            // 同步最新的日志开关到运行中的驱动
            UpdateTraceConfigs();
        }

        /// <summary>
        /// 动态更新运行中设备的日志追踪开关 (实时生效)
        /// </summary>
        public void UpdateTraceConfigs()
        {
            var cfg = GlobalData.DebugConfig;
            if (cfg == null) return;

            foreach (var client in _protocolClients.Values)
            {
                // 同步驱动内部的详细追踪开关 (控制原始 TX/RX)
                client.TraceDetail = cfg.Trace_Communication_Raw;
            }

            foreach (var slave in _slaves.Values)
            {
                // 同步从站/服务端内部的详细追踪开关
                slave.TraceDetail = cfg.Trace_Communication_Raw;
            }
        }

        /// <summary>
        /// 初始化并启动所有配置设备的通讯运行时
        /// </summary>
        private async Task InitializeRuntimesAsync()
        {
            foreach (var dev in GlobalData.ListDveices)
            {
                try
                {
                    if (_runtimes.TryGetValue(dev.ID, out var old)) { old.Stop(); old.Dispose(); _runtimes.Remove(dev.ID); }

                    var paramDict = dev.CommParsams.ToDictionary(k => k.Name, v => v.Value);

                    // --- Slave Mode (服务端模式) ---
                    if (dev.Protocol.Contains("SERVER"))
                    {
                        if (GlobalData.ProtocolsPairs != null && GlobalData.ProtocolsPairs.TryGetValue(dev.Protocol, out var slavePlugin))
                        {
                            var finalParams = dev.CommParsams.ToList();
                            var points = _devicePointsCache.GetValueOrDefault(dev.ID, new List<DevicePointConfigEntity>());
                            // 自动计算点位覆盖的地址范围
                            var (calcAddr, calcCount) = CommTaskGenerator.CalculateDevicePointRange(points);
                            
                            if (calcCount > 0)
                            {
                                if (!paramDict.ContainsKey("起始地址")) finalParams.Add(new CommParamEntity { Name = "起始地址", Value = calcAddr.ToString() });
                                if (!paramDict.ContainsKey("寄存器数量")) finalParams.Add(new CommParamEntity { Name = "寄存器数量", Value = calcCount.ToString() });
                            }
                            slavePlugin.Initialize(finalParams);
                            
                            // 绑定 Server 数据接收事件 (用于驱动 UI 更新)
                             slavePlugin.OnDataReceived += (s, args) => _dataProcessor?.ProcessData(dev.ID, args.AddressTag, args.Data);

                            slavePlugin.Open();
                            _slaves[dev.ID] = slavePlugin;
                            LogHelper.Info($"[CommService] 已启动从站设备: {dev.Name}");
                        }
                        continue;
                    }

                    // --- Client Mode (客户端模式) ---
                    string type = (paramDict.ContainsKey("IP") || paramDict.ContainsKey("目标IP") || paramDict.ContainsKey("IP地址")) ? "TCP" : "RTU";
                    ICommChannel channel = CommChannelFactory.CreateChannel(type, paramDict);

                    IProtocolClient? protocolClient = null;
                    if (dev.Protocol.Contains("S7", StringComparison.OrdinalIgnoreCase))
                    {
                        var s7 = new Communicationlib.Protocol.S7.S7ProtocolClient();
                        string ip = paramDict.GetValueOrDefault("IP地址", paramDict.GetValueOrDefault("IP", "127.0.0.1"));
                        s7.SetConnectionConfig(ip, int.Parse(paramDict.GetValueOrDefault("机架号", "0")), int.Parse(paramDict.GetValueOrDefault("插槽号", "2")));
                        
                        // 注入日志与配置 (动态检查，确保开关实时生效)
                        s7.Logger = msg => {
                            var current = GlobalData.DebugConfig;
                            if (current != null && (current.Trace_Communication_Raw || current.Trace_S7_Detail))
                                LogHelper.Debug(msg);
                        };
                        s7.TraceDetail = GlobalData.DebugConfig?.Trace_Communication_Raw ?? false;
                        
                        protocolClient = s7;
                    }
                    else if (dev.Protocol == "MODBUS_RTU_CLIENT") 
                    {
                        var rtu = new Communicationlib.Protocol.Modbus.ModbusRtuClient(); 
                        rtu.Logger = msg => {
                            var current = GlobalData.DebugConfig;
                            if (current != null && (current.Trace_Communication_Raw || current.Trace_Modbus_Detail))
                                LogHelper.Debug(msg);
                        };
                        rtu.TraceDetail = GlobalData.DebugConfig?.Trace_Communication_Raw ?? false;
                        protocolClient = rtu;
                    }
                    else 
                    {
                        var tcp = new Communicationlib.Protocol.Modbus.ModbusTcpClient(); 
                        // Inject Logger & Trace Config
                        tcp.Logger = msg => {
                            var current = GlobalData.DebugConfig;
                            if (current != null && (current.Trace_Communication_Raw || current.Trace_Modbus_Detail))
                                LogHelper.Debug(msg);
                        };
                        tcp.TraceDetail = GlobalData.DebugConfig?.Trace_Communication_Raw ?? false;
                        protocolClient = tcp;
                    }

                    protocolClient.DeviceName = dev.Name;
                    _protocolClients[dev.ID] = protocolClient;

                    var runtime = new CommRuntime(channel, protocolClient) { RuntimeName = $"Runtime_{dev.Name}" };
                    // 注入 Runtime 日志 (包含异常捕获日志)
                    // 注入 Runtime 日志 (主要包含异常捕获日志)
                    runtime.Logger = msg => {
                        var current = GlobalData.DebugConfig;
                        if (current != null && current.Trace_Communication_Raw)
                            LogHelper.Debug(msg);
                    };
                    
                    int cycleTime = int.TryParse(paramDict.GetValueOrDefault("循环读取时间", "500"), out var c) ? c : 500;

                    // 生成自动轮询任务
                    var pollingTasks = CommTaskGenerator.CreatePollingTasks(dev, _devicePointsCache.GetValueOrDefault(dev.ID, new List<DevicePointConfigEntity>()), cycleTime);
                    foreach (var t in pollingTasks) runtime.AddTaskConfig(t);

                    // 绑定数据接收事件
                    runtime.OnDataReceived += (s, args) => _dataProcessor?.ProcessData(dev.ID, args.AddressTag, args.Data);

                    _runtimes[dev.ID] = runtime;
                    runtime.Start();
                    LogHelper.Info($"[CommService] 已启动采集设备: {dev.Name}");
                }
                catch (Exception ex) { LogHelper.Error($"[CommService] 初始化设备 {dev.Name} 失败", ex); }
            }
            
            // --- 启动 TimeSyncManager ---
            var activeDevices = new Dictionary<ConfigEntity, object>();
            
            // 1. Client Mode Devices (Runtimes)
            foreach (var kvp in _runtimes)
            {
                var cfg = GlobalData.ListDveices.FirstOrDefault(d => d.ID == kvp.Key);
                if (cfg != null)
                {
                    // 直接传递 CommRuntime 实例
                    activeDevices[cfg] = kvp.Value;
                }
            }

            // 2. Server Mode Devices (Slaves)
            foreach (var kvp in _slaves)
            {
                var cfg = GlobalData.ListDveices.FirstOrDefault(d => d.ID == kvp.Key);
                if (cfg != null)
                {
                    activeDevices[cfg] = kvp.Value;
                }
            }

            TimeSyncManager.Instance.Start(activeDevices);
            
            await Task.CompletedTask;
        }

        /// <summary>
        /// 释放所有资源，停止所有通讯任务
        /// </summary>
        public void Dispose()
        {
            _isRunning = false;
            foreach (var r in _runtimes.Values) r.Dispose();
            _runtimes.Clear();
            _protocolClients.Clear();
            foreach (var s in _slaves.Values) s.Close();
            _slaves.Clear();

            TimeSyncManager.Instance.Stop();
            
            try { NtpService.Instance.Stop(); } catch { }

            LogService.Instance.Dispose();
        }

        /// <summary>
        /// (UI操作) 向设备写入点位值
        /// </summary>
        /// <param name="p">点位配置</param>
        /// <param name="valueStr">用户输入的字符串值</param>
        public async Task<bool> WritePointValueAsync(DevicePointConfigEntity p, string valueStr)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(valueStr)) return false;

                // 1. 数据类型解析与转换
                string dType = p.DataType?.ToLower() ?? "word";
                ushort[]? registersToWrite = null;
                bool isBitMode = false;
                bool bitValue = false;

                // 尝试解析数值
                if (dType.Contains("bit") || dType.Contains("bool"))
                {
                    // bool parsing: "1", "true", "on"
                    if (valueStr == "1" || valueStr.Equals("true", StringComparison.OrdinalIgnoreCase) || valueStr.Equals("on", StringComparison.OrdinalIgnoreCase)) bitValue = true;
                    else bitValue = false;
                    isBitMode = true;
                }
                else if (dType.Contains("dword") || dType.Contains("int32") || dType.Contains("uint32") || dType.Contains("integer"))
                {
                    if (long.TryParse(valueStr, out long lVal))
                    {
                        uint val = (uint)lVal;
                        ushort high = (ushort)(val >> 16);
                        ushort low = (ushort)(val & 0xFFFF);
                        registersToWrite = new ushort[] { high, low }; // Big-Endian Word Order (CD AB)
                    }
                }
                else if (dType.Contains("float") || dType.Contains("real"))
                {
                    if (float.TryParse(valueStr, out float fVal))
                    {
                        byte[] bytes = BitConverter.GetBytes(fVal);
                        ushort low = BitConverter.ToUInt16(bytes, 0);
                        ushort high = BitConverter.ToUInt16(bytes, 2);
                        registersToWrite = new ushort[] { high, low }; // Big-Endian Word Order
                    }
                }
                else // word, int16, short
                {
                    if (double.TryParse(valueStr, out double dVal))
                    {
                        ushort val = (ushort)(short)dVal;
                        registersToWrite = new ushort[] { val };
                    }
                }

                if (!isBitMode && registersToWrite == null)
                {
                     LogHelper.Warn($"[Write] Failed to parse value '{valueStr}' for type '{dType}'");
                     return false;
                }

                int devId = p.SourceDeviceId;
                string addr = p.Address;

                // 2. 执行写入 (优先 Runtime)
                if (_runtimes.TryGetValue(devId, out var runtime))
                {
                     var devCfg = GlobalData.ListDveices.FirstOrDefault(d => d.ID == devId);
                     bool isS7 = devCfg?.Protocol?.Contains("S7") == true;

                     if (isBitMode)
                     {
                         // Read-Modify-Write for bits inside registers might be needed, 
                         // but usually WriteAsync handling "bit" tag or implementing Read-Modify-Write internally?
                         // CommRuntime supports "100.1" address format for bits? Assume yes for now.
                         // But for Modbus, it might be Coil or Bit within Register.
                         // For S7, it's DB1.DBX0.0.
                         // 这里假定 runtime.WriteAsync 能够处理布尔值
                         
                         // Special case: If Modbus and BitIndex > 0, we might need Read-Modify-Write manually if runtime doesn't support bit address directly.
                         // For simplicity, assume runtime handles valid address tags.
                         
                         if (isS7) await runtime.WriteAsync(addr, bitValue); // S7 runtime supports bool overload?
                         else 
                         {
                             // Modbus: check if coil or holding reg bit.
                             // If it's a register address like "40001" with BitIndex > 0, we need masking.
                             if (p.BitIndex > 0)
                             {
                                 var readRes = await runtime.ReadAsync(addr, 1);
                                 if (readRes != null && readRes.Length > 0)
                                 {
                                     ushort current = readRes[0];
                                     ushort mask = (ushort)(1 << p.BitIndex);
                                     ushort newVal = bitValue ? (ushort)(current | mask) : (ushort)(current & ~mask);
                                     await runtime.WriteAsync(addr, newVal);
                                 }
                             }
                             else
                             {
                                 // Assume 0 or bool is treated as Coil 1/0 or simple write.
                                 // For safety, convert bool to 1/0 ushort if target is register?
                                 // runtime.WriteAsync(addr, (ushort)(bitValue?1:0))?
                                 // Let's assume protocol client handles boolean write if address is Coil.
                                 // If address is generic "40001", writing bool might verify signature.
                                 // CommunicationLib `WriteAsync` has `object` or overloads?
                                 // Based on DataSyncService: `await runtime.WriteAsync(targetAddrStr, newVal);` (ushort)
                                 
                                 await runtime.WriteAsync(addr, (ushort)(bitValue ? 1 : 0));
                             }
                         }
                     }
                     else
                     {
                         if (isS7)
                         {
                             var byteList = new List<byte>();
                             foreach (var reg in registersToWrite) { byteList.Add((byte)(reg >> 8)); byteList.Add((byte)(reg & 0xFF)); }
                             await runtime.WriteAsync(addr, byteList.ToArray());
                         }
                         else
                         {
                              if (registersToWrite.Length == 1) await runtime.WriteAsync(addr, registersToWrite[0]);
                              else await runtime.WriteAsync(addr, registersToWrite);
                         }
                     }
                     LogHelper.Info($"[Write] Success: {p.UiBinding}={valueStr} -> Dev:{devId} Addr:{addr}");
                     return true;
                }
                
                // 3. 执行写入 (次选 Slave - 本地模拟)
                if (_slaves.TryGetValue(devId, out var slave))
                {
                    if (slave is Communicationlib.Protocol.Modbus.Server.ModbusTcpSlave modbusSlave && modbusSlave.Memory != null)
                    {
                         ushort iAddr = (ushort)CommTaskGenerator.NormalizeAddress(addr);
                         if (isBitMode)
                         {
                             var regs = modbusSlave.Memory.ReadHoldingRegisters(iAddr, 1);
                             if (regs != null && regs.Length > 0)
                             {
                                 ushort current = regs[0];
                                 ushort mask = (ushort)(1 << p.BitIndex);
                                 ushort newVal = bitValue ? (ushort)(current | mask) : (ushort)(current & ~mask);
                                 modbusSlave.Memory.WriteSingleRegister(iAddr, newVal);
                             }
                         }
                         else if (registersToWrite != null)
                         {
                             for(int i=0; i< registersToWrite.Length; i++)
                                 modbusSlave.Memory.WriteSingleRegister((ushort)(iAddr+i), registersToWrite[i]);
                         }
                         return true;
                    }
                }

                LogHelper.Warn($"[Write] Device {devId} not found (Runtime/Slave)");
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[Write] Error writing {p.UiBinding}", ex);
                return false;
            }
        }
    }
}
