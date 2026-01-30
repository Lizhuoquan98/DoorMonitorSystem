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
        private ConcurrentBag<DevicePointConfigEntity> _pointConfigs = new();
        private bool _isRunning = false;

        private readonly Dictionary<int, CommRuntime> _runtimes = new();
        private readonly Dictionary<int, ICommBase> _slaves = new();
        private Dictionary<int, List<DevicePointConfigEntity>> _devicePointsCache = new();
        
        private DataProcessor? _dataProcessor;

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
                    _devicePointsCache = points.GroupBy(p => p.SourceDeviceId).ToDictionary(g => g.Key, g => g.ToList());
                    LogHelper.Info($"[CommService] 从数据库加载了 {points.Count} 个点位配置");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[CommService] 加载点位配置错误 (LoadPointConfigs)", ex);
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
                        
                        // 注入日志与配置
                        s7.Logger = msg => LogHelper.Debug(msg);
                        s7.TraceDetail = GlobalData.DebugConfig?.Trace_S7_Detail ?? false;
                        
                        protocolClient = s7;
                    }
                    else if (dev.Protocol == "MODBUS_RTU_CLIENT") 
                    {
                        var rtu = new Communicationlib.Protocol.Modbus.ModbusRtuClient(); 
                        rtu.Logger = msg => LogHelper.Debug(msg);
                        rtu.TraceDetail = GlobalData.DebugConfig?.Trace_Communication_Raw ?? false;
                        protocolClient = rtu;
                    }
                    else 
                    {
                        var tcp = new Communicationlib.Protocol.Modbus.ModbusTcpClient(); 
                        // Inject Logger & Trace Config
                        tcp.Logger = msg => LogHelper.Debug(msg);
                        tcp.TraceDetail = GlobalData.DebugConfig?.Trace_Communication_Raw ?? false;
                        protocolClient = tcp;
                    }

                    protocolClient.DeviceName = dev.Name;
                    
                    var runtime = new CommRuntime(channel, protocolClient) { RuntimeName = $"Runtime_{dev.Name}" };
                    // 注入 Runtime 日志 (包含异常捕获日志)
                    runtime.Logger = msg => LogHelper.Debug(msg);
                    
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
            foreach (var s in _slaves.Values) s.Close();
            _slaves.Clear();

            TimeSyncManager.Instance.Stop();
            
            try { NtpService.Instance.Stop(); } catch { }

            LogService.Instance.Dispose();
        }
    }
}
