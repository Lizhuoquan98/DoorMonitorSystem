using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ControlLibrary.Models;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.ViewModels;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.system;
using Communicationlib.config;
using DoorMonitorSystem.Models;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 全局数据管理中心 (Data Brain)
    /// 负责：
    /// 1. 加载所有配置文件 (JSON/DB)
    /// 2. 维护点位运行时状态与缓存
    /// 3. 执行业务逻辑裁决 (Visual Adjudication)
    /// 4. 驱动 UI 与通讯层的业务同步
    /// </summary>
    public class DataManager : IDisposable
    {
        private static readonly Lazy<DataManager> _instance = new(() => new DataManager());
        public static DataManager Instance => _instance.Value;

        // 后台任务统一取消令牌（批量更新循环 + 日志清理调度器）
        private readonly CancellationTokenSource _backgroundCts = new CancellationTokenSource();
        private bool _disposed = false;

        // 高性能索引：[设备ID] -> [归一化地址] -> [该地址下的点位列表]
        private Dictionary<int, Dictionary<int, List<DevicePointConfigEntity>>> _addressIndex = new();

        // 视觉资源共享池：避免 1,500 个门重复克隆相同的图标集
        private readonly Dictionary<string, List<ControlLibrary.Models.IconItem>> _sharedIconCache = new();
        private readonly object _cacheLock = new object();

        // 点位索引缓存，提高通讯更新效率
        private readonly Dictionary<string, DoorBitConfig> _doorBitCache = new();
        private readonly Dictionary<string, PanelBitConfig> _panelBitCache = new();
        // 门模型缓存，用于快速查找并刷新视觉裁决
        private readonly Dictionary<string, DoorModel> _doorCache = new();
        
        private bool _isCacheBuilt = false;
        private bool _debugKeysPrinted = false;

        /// <summary>
        /// 交互优先模式：当用户拖动弹窗时，暂停数据刷新以保证流畅度
        /// </summary>
        public volatile bool IsUIInteractionActive = false;

        private DataManager() { }

        /// <summary>
        /// 第一阶段初始化：加载基础配置文件
        /// 应该在程序启动最早期调用 (MainWindow 构造函数)
        /// </summary>
        public void Initialize()
        {
            try
            {
                // 1. 加载系统连接配置 (Bootstrapping)
                LoadSystemConfig();

                // 2. 加载数据库中的全局设置 (Debug, NTP) - 替代原有的 LoadDebugConfig
                LoadGlobalSettings();

                // 3. 加载图形和设备 (DB > JSON)
                LoadGraphicDictionary();
                LoadDevices();
                
                // 确保日志数据库和本月表存在 (如果是首次运行)
                
                // 确保日志数据库和本月表存在 (如果是首次运行)
                _ = Task.Run(() => EnsureLogDatabaseExists());

                // 启动日志自动清理调度（包含启动时立即执行一次）
                StartLogCleanupScheduler();
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 初始化失败", ex);
            }
        }

        /// <summary>
        /// 第二阶段初始化：加载业务数据 (站台/门/面板)
        /// 应该在 MainViewModel 准备好后调用
        /// </summary>
        public async Task LoadBusinessDataAsync()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;

                string connStr = $"Server={GlobalData.SysCfg.ServerAddress};" +
                               $"Database={GlobalData.SysCfg.DatabaseName};" +
                               $"User ID={GlobalData.SysCfg.UserName};" +
                               $"Password={GlobalData.SysCfg.UserPassword};" +
                               $"CharSet=utf8mb4;";

                // 确保业务数据库结构最新 (包含 KeyId 迁移)
                BusinessDatabaseFixer.FixSchema(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);

                var stationService = new StationDataService(connStr);
                var stations = await Task.Run(() => stationService.LoadAllStations());

                if (GlobalData.MainVm != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        GlobalData.MainVm.Stations.Clear();
                        foreach (var s in stations)
                        {
                            GlobalData.MainVm.Stations.Add(new StationViewModel(s));
                        }
                    });
                    
                    BuildBitCache();

                    // ★ 强烈建议：加载后立即对所有门执行一次初始视觉裁决
                    // 确保首屏即使没有点位变化也能显示默认状态（如“关闭”）
                    foreach (var door in _doorCache.Values)
                    {
                        AdjudicateDoorVisual(door);
                    }

                    LogHelper.Info($"[DataManager] 业务数据加载完成并完成初始裁决. 站台数量: {stations.Count}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 加载业务数据 (LoadBusinessDataAsync) 失败", ex);
            }
        }

        #region 配置加载逻辑 (JSON)

        /// <summary>
        /// 加载系统配置文件 (SystemConfig.json)
        /// 包含数据库连接字符串、服务器端口等基础配置
        /// </summary>
        /// <summary>
        /// 加载系统配置文件 (SystemConfig.txt > SystemConfig.json)
        /// </summary>
        private void LoadSystemConfig()
        {
            try
            {
                string txtPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SystemConfig.txt");
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SystemConfig.json");

                // 1. 优先尝试读取 TXT
                if (File.Exists(txtPath))
                {
                    LogHelper.Info("[DataManager] 正在加载 SystemConfig.txt ...");
                    GlobalData.SysCfg = new SysCfg();
                    var lines = File.ReadAllLines(txtPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim();
                            string val = parts[1].Trim();
                            
                            switch (key)
                            {
                                case "ServerAddress": GlobalData.SysCfg.ServerAddress = val; break;
                                case "UserName": GlobalData.SysCfg.UserName = val; break;
                                case "UserPassword": GlobalData.SysCfg.UserPassword = val; break;
                                case "DatabaseName": GlobalData.SysCfg.DatabaseName = val; break;
                                case "MonitorIndex": 
                                    if (int.TryParse(val, out int idx)) GlobalData.SysCfg.MonitorIndex = idx; 
                                    break;
                                case "StationName": GlobalData.SysCfg.StationName = val; break;
                            }
                        }
                    }
                    LogHelper.Info("[DataManager] SystemConfig.txt 加载成功。");
                    return;
                }

                // 2. 如果 TXT 不存在，则回退读取 JSON，并自动生成 TXT
                if (File.Exists(jsonPath))
                {
                    LogHelper.Info("[DataManager] 未找到 TXT，正在加载 SystemConfig.json ...");
                    string json = File.ReadAllText(jsonPath);
                    GlobalData.SysCfg = JsonSerializer.Deserialize<SysCfg>(json);

                    if (GlobalData.SysCfg != null)
                    {
                        // 自动在此处生成 TXT，方便用户后续修改
                        try 
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine("ServerAddress=" + GlobalData.SysCfg.ServerAddress);
                            sb.AppendLine("UserName=" + GlobalData.SysCfg.UserName);
                            sb.AppendLine("UserPassword=" + GlobalData.SysCfg.UserPassword);
                            sb.AppendLine("DatabaseName=" + GlobalData.SysCfg.DatabaseName);
                            sb.AppendLine("MonitorIndex=" + GlobalData.SysCfg.MonitorIndex);
                            sb.AppendLine("StationName=" + GlobalData.SysCfg.StationName);
                            
                            File.WriteAllText(txtPath, sb.ToString(), System.Text.Encoding.UTF8);
                            LogHelper.Info($"[DataManager] 已自动从 JSON 生成配置文件: {txtPath}");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error("[DataManager] 自动生成 SystemConfig.txt 失败", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 加载系统配置失败", ex);
            }
        }
        
        /// <summary>
        /// 加载全局设置 (Debug, NTP) 优先从 DB 加载
        /// </summary>
        private void LoadGlobalSettings()
        {
            // 初始化默认值
            GlobalData.DebugConfig = new AppDebugConfig();
            GlobalData.NtpConfig = new NtpConfig();

            try
            {
                if (GlobalData.SysCfg != null)
                {
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    try { db.Connect(); } catch { }

                    if (db.IsConnected)
                    {
                        var settings = db.Query<SysSettingsEntity>("SysSettingsEntity", "1=1", null);
                        if (settings != null && settings.Count > 0)
                        {
                            foreach (var s in settings)
                            {
                                switch(s.SettingKey)
                                {
                                    // Debug
                                    case "Debug.TraceS7": GlobalData.DebugConfig.Trace_S7_Detail = bool.Parse(s.SettingValue); break;
                                    case "Debug.TraceModbus": GlobalData.DebugConfig.Trace_Modbus_Detail = bool.Parse(s.SettingValue); break;
                                    case "Debug.TraceForward": GlobalData.DebugConfig.Trace_Forwarding_Detail = bool.Parse(s.SettingValue); break;
                                    case "Debug.TraceRaw": GlobalData.DebugConfig.Trace_Communication_Raw = bool.Parse(s.SettingValue); break;
                                    case "Debug.LogRetentionMonths": GlobalData.DebugConfig.LogRetentionMonths = int.Parse(s.SettingValue); break;
                                    case "Debug.LogCleanupTime": GlobalData.DebugConfig.LogCleanupTime = s.SettingValue; break;
                                    
                                    // NTP
                                    case "Ntp.ClientEnabled": GlobalData.NtpConfig.IsNtpClientEnabled = bool.Parse(s.SettingValue); break;
                                    case "Ntp.ServerUrl": GlobalData.NtpConfig.NtpServerUrl = s.SettingValue; break;
                                    case "Ntp.Interval": GlobalData.NtpConfig.NtpSyncIntervalMinutes = int.Parse(s.SettingValue); break;
                                    case "Ntp.ServerEnabled": GlobalData.NtpConfig.IsNtpServerEnabled = bool.Parse(s.SettingValue); break;
                                }
                            }
                            // Apply helper logic
                            Communicationlib.Protocol.Modbus.ModbusRawLogger.IsEnabled = GlobalData.DebugConfig.Trace_Communication_Raw;
                            LogHelper.Info($"[DataManager] 全局设置已从数据库加载 (Count: {settings.Count})");
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 从数据库加载全局设置失败，尝试使用默认值", ex);
            }

            // Fallback: 如果 DB 没数据，也依然尝试从文件读一下 (为了兼容性) 或保持默认
            // LoadDebugConfig(); // 原有方法，如果需要保留文件兜底可放开，但设计目的是完全迁移
        }

        private void StartLogCleanupScheduler()
        {
            var token = _backgroundCts.Token;
            Task.Run(async () =>
            {
                // 1. 启动时立即尝试执行一次 (确保即便夜间没开机，开机也会清)
                CleanupOldLogTables();

                string lastRunDate = DateTime.Now.ToString("yyyy-MM-dd");

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), token); // 每分钟检测一次

                        string configTime = GlobalData.DebugConfig?.LogCleanupTime ?? "01:00:00";
                        if (TimeSpan.TryParse(configTime, out TimeSpan targetTime))
                        {
                            var now = DateTime.Now;
                            // 如果当前时间超过了目标时间，且今天还没运行过
                            if (now.TimeOfDay >= targetTime && lastRunDate != now.ToString("yyyy-MM-dd"))
                            {
                                CleanupOldLogTables();
                                lastRunDate = now.ToString("yyyy-MM-dd");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break; // 正常退出
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("[DataManager] 定时清理任务调度异常", ex);
                    }
                }
            }, token);
        }

        private void EnsureLogDatabaseExists()
        {
            if (GlobalData.SysCfg == null) return;
            LogHelper.Info("[DataManager] 正在检查数据库运行环境...");
            
            // 1. 检查并确保主业务数据库存在
            try
            {
                using var dbMain = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                dbMain.Connect(); // 内部自动建库
                LogHelper.Info($"[DataManager] 主业务数据库校验通过: {GlobalData.SysCfg.DatabaseName}");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[DataManager] 主业务数据库初始化失败: {GlobalData.SysCfg.DatabaseName}", ex);
            }

            // 2. 检查并确保日志数据库存在
            try
            {
                string logDb = GlobalData.SysCfg.LogDatabaseName;
                using var dbLog = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                dbLog.Connect(); 
                
                // 顺便确保本月的表也存在，方便用户在数据库管理工具中看到
                string tableName = $"PointLogs_{DateTime.Now:yyyyMM}";
                dbLog.CreateTableFromModel<PointLogEntity>(tableName);
                LogHelper.Info($"[DataManager] 日志数据库校验通过: {logDb}, 本月表: {tableName}");
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[DataManager] 日志数据库初始化失败: {GlobalData.SysCfg.LogDatabaseName}", ex);
            }

            // 3. 检查并确保用户表存在 (在主数据库中)
            try
            {
                using var dbMain = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                dbMain.Connect();
                dbMain.CreateTableFromModel<UserEntity>(); // Sys_Users
                
                // 检查是否需要创建默认管理员
                var admin = dbMain.Query<UserEntity>("Sys_Users", "Username = @u", new MySql.Data.MySqlClient.MySqlParameter("@u", "admin"));
                if (admin == null || admin.Count == 0)
                {
                    var defaultUser = new UserEntity
                    {
                        Username = "admin",
                        Password = CryptoHelper.ComputeMD5("123"), // MD5 哈希，默认密码 123
                        RealName = "System Administrator",
                        Role = "Admin",
                        IsEnabled = true
                    };
                    dbMain.Insert(defaultUser);
                    LogHelper.Info("[DataManager] 已创建默认管理员账号 (密码已 MD5 哈希)");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 用户表初始化失败", ex);
            }

            // 4. 确保操作日志表存在 (Sys_OperationLogs)
            try
            {
                 using var dbMain = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                 dbMain.Connect();
                 dbMain.CreateTableFromModel<OperationLogEntity>();
            }
            catch (Exception ex)
            {
                 LogHelper.Error("[DataManager] 操作日志表初始化失败", ex);
            }

            // 5. 确保配置表存在 (Sys_Devices, Sys_GraphicGroups...)
            // 修复：在数据交互前，先执行一次 Schema 检查与修复，确保主键存在
            try
            {
                 DatabaseFixer.FixSchema(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 数据库Schema修复尝试失败 (非致命)", ex);
            }

            // 并执行 JSON -> DB 的迁移
            try
            {
                MigrateConfigurationToDb();
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 配置数据迁移失败", ex);
            }
        }

        /// <summary>
        /// 检查配置表是否存在数据，如果为空则从 JSON 迁移
        /// </summary>
        private void MigrateConfigurationToDb()
        {
            if (GlobalData.SysCfg == null) return;
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            db.Connect();

            // 1. 创建表结构
            db.CreateTableFromModel<SysDeviceEntity>();
            db.CreateTableFromModel<SysDeviceParamEntity>();
            db.CreateTableFromModel<SysGraphicGroupEntity>();
            db.CreateTableFromModel<SysGraphicItemEntity>();
            db.CreateTableFromModel<SysSettingsEntity>();
            
            // 5. ASD 参数系统表
            db.CreateTableFromModel<AsdModelMappingEntity>();
            db.CreateTableFromModel<ParameterDefineEntity>();

            // 2. 迁移设备数据
            var deviceCount = db.Query<SysDeviceEntity>("SysDeviceEntity", "1=1", null)?.Count ?? 0;
            if (deviceCount == 0)
            {
                LogHelper.Info("[DataManager] 检测到设备表为空，开始从 devices.json 迁移...");
                MigrateDevicesFromJson(db);
            }

            // 3. 迁移图形数据
            var graphicCount = db.Query<SysGraphicGroupEntity>("SysGraphicGroupEntity", "1=1", null)?.Count ?? 0;
            if (graphicCount == 0)
            {
                LogHelper.Info("[DataManager] 检测到图形库为空，开始从 GraphicDictionary.json 迁移...");
                MigrateGraphicsFromJson(db);
            }

            // 4. 迁移通用设置 (Debug, NTP)
            var settingsCount = db.Query<SysSettingsEntity>("SysSettingsEntity", "1=1", null)?.Count ?? 0;
            if (settingsCount == 0)
            {
                LogHelper.Info("[DataManager] 检测到设置表为空，开始从 JSON 迁移通用配置...");
                MigrateSettingsFromJson(db);
            }

            // 5. 迁移 ASD 模型映射
            var asdModelCount = db.Query<AsdModelMappingEntity>("Sys_AsdModels", "1=1", null)?.Count ?? 0;
            if (asdModelCount == 0)
            {
                LogHelper.Info("[DataManager] 检测到 ASD 模型映射表为空，开始初始化...");
                MigrateAsdModelsToDb(db);
            }

            // 6. 迁移 ASD 参数定义
            var paramDefineCount = db.Query<ParameterDefineEntity>("Sys_ParameterDefines", "1=1", null)?.Count ?? 0;
            if (paramDefineCount == 0)
            {
                LogHelper.Info("[DataManager] 检测到 ASD 参数定义表为空，开始初始化...");
                MigrateParameterDefinesToDb(db);
            }
        }

        private void MigrateSettingsFromJson(SQLHelper db)
        {
            // 1. DebugConfig
            string debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "DebugConfig.json");
            if (File.Exists(debugPath))
            {
                try
                {
                    var json = File.ReadAllText(debugPath);
                    var cfg = JsonSerializer.Deserialize<AppDebugConfig>(json);
                    if (cfg != null)
                    {
                        var settings = new List<SysSettingsEntity>
                        {
                            new SysSettingsEntity { Category="Debug", SettingKey="Debug.TraceS7", SettingValue=cfg.Trace_S7_Detail.ToString(), Description="S7详细日志" },
                            new SysSettingsEntity { Category="Debug", SettingKey="Debug.TraceModbus", SettingValue=cfg.Trace_Modbus_Detail.ToString(), Description="Modbus详细日志" },
                            new SysSettingsEntity { Category="Debug", SettingKey="Debug.TraceForward", SettingValue=cfg.Trace_Forwarding_Detail.ToString(), Description="转发详细日志" },
                            new SysSettingsEntity { Category="Debug", SettingKey="Debug.TraceRaw", SettingValue=cfg.Trace_Communication_Raw.ToString(), Description="原始报文日志" },
                            new SysSettingsEntity { Category="Debug", SettingKey="Debug.LogRetentionMonths", SettingValue=cfg.LogRetentionMonths.ToString(), Description="日志保留月数" },
                            new SysSettingsEntity { Category="Debug", SettingKey="Debug.LogCleanupTime", SettingValue=cfg.LogCleanupTime, Description="日志清理时间" }
                        };
                        foreach(var s in settings) db.Insert(s);
                    }
                }
                catch { /* Ignore migration errors */ }
            }

            // 2. NtpConfig
            string ntpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NtpConfig.json");
            if (File.Exists(ntpPath))
            {
                try
                {
                    var json = File.ReadAllText(ntpPath);
                    var cfg = JsonSerializer.Deserialize<NtpConfig>(json);
                    if (cfg != null)
                    {
                        var settings = new List<SysSettingsEntity>
                        {
                            new SysSettingsEntity { Category="Ntp", SettingKey="Ntp.ClientEnabled", SettingValue=cfg.IsNtpClientEnabled.ToString(), Description="启用NTP客户端" },
                            new SysSettingsEntity { Category="Ntp", SettingKey="Ntp.ServerUrl", SettingValue=cfg.NtpServerUrl, Description="NTP服务器地址" },
                            new SysSettingsEntity { Category="Ntp", SettingKey="Ntp.Interval", SettingValue=cfg.NtpSyncIntervalMinutes.ToString(), Description="同步间隔(分)" },
                            new SysSettingsEntity { Category="Ntp", SettingKey="Ntp.ServerEnabled", SettingValue=cfg.IsNtpServerEnabled.ToString(), Description="启用NTP服务端" }
                        };
                        foreach(var s in settings) db.Insert(s);
                    }
                }
                catch { /* Ignore */ }
            }
        }

        private void MigrateAsdModelsToDb(SQLHelper db)
        {
            var list = new List<AsdModelMappingEntity>
            {
                new AsdModelMappingEntity { DisplayName = "ASD1", PlcId = 1 },
                new AsdModelMappingEntity { DisplayName = "ASD2", PlcId = 24 },
                new AsdModelMappingEntity { DisplayName = "ASD3", PlcId = 32 }
            };
            foreach (var item in list) db.Insert(item);
        }

        private void MigrateParameterDefinesToDb(SQLHelper db)
        {
            var list = new List<ParameterDefineEntity>
            {
                new ParameterDefineEntity { Label = "最大开门速度", Unit = "mm/s", Hint = "0-10000", BindingKey = "MaxSpeedOpen", SortOrder = 1, PlcPermissionValue = 3, DataType = "Int16" },
                new ParameterDefineEntity { Label = "最大关门速度", Unit = "mm/s", Hint = "0-10000", BindingKey = "MaxSpeedClose", SortOrder = 2, PlcPermissionValue = 3, DataType = "Int16" },
                new ParameterDefineEntity { Label = "最大开门力", Unit = "N", Hint = "100-500", BindingKey = "MaxForceOpen", SortOrder = 3, PlcPermissionValue = 2, DataType = "Int16" },
                new ParameterDefineEntity { Label = "最大关门力", Unit = "N", Hint = "100-500", BindingKey = "MaxForceClose", SortOrder = 4, PlcPermissionValue = 2, DataType = "Int16" },
                new ParameterDefineEntity { Label = "关门违阻重试次数", Unit = "次", Hint = "0-10", BindingKey = "CloseObstructRetry", SortOrder = 5, PlcPermissionValue = 1, DataType = "UInt16" },
                new ParameterDefineEntity { Label = "关门违阻反向时间", Unit = "ms", Hint = "0-10000", BindingKey = "CloseObstructRevTime", SortOrder = 6, PlcPermissionValue = 1, DataType = "Int16" },
                new ParameterDefineEntity { Label = "关门违阻反向距离", Unit = "mm", Hint = "0-10000", BindingKey = "CloseObstructRevDist", SortOrder = 7, PlcPermissionValue = 1, DataType = "Int16" },
                new ParameterDefineEntity { Label = "手动解锁释放时间", Unit = "ms", Hint = "0-30000", BindingKey = "ManualUnlockTime", SortOrder = 8, PlcPermissionValue = 1, DataType = "UInt32" }
            };
            foreach (var item in list) db.Insert(item);
        }

        /// <summary>
        /// 从数据库加载 ASD 模型映射
        /// </summary>
        public List<AsdModelMappingEntity> LoadAsdModelsFromDb()
        {
            if (GlobalData.SysCfg == null) return new List<AsdModelMappingEntity>();
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            db.Connect();
            return db.Query<AsdModelMappingEntity>("Sys_AsdModels", "1=1 ORDER BY DisplayName", null) ?? new List<AsdModelMappingEntity>();
        }

        /// <summary>
        /// 更新或插入 ASD 模型映射
        /// </summary>
        public void SaveAsdModel(AsdModelMappingEntity entity)
        {
            if (GlobalData.SysCfg == null) return;
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            db.Connect();
            if (entity.Id > 0) db.Update(entity);
            else {
                if (string.IsNullOrEmpty(entity.KeyId)) entity.KeyId = Guid.NewGuid().ToString();
                db.Insert(entity);
            }
        }

        /// <summary>
        /// 删除 ASD 模型映射
        /// </summary>
        public void DeleteAsdModel(AsdModelMappingEntity entity)
        {
            if (GlobalData.SysCfg == null || entity.Id <= 0) return;
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            db.Connect();
            db.Delete(entity);
        }

        /// <summary>
        /// 从数据库加载 ASD 参数定义
        /// </summary>
        public List<ParameterDefineEntity> LoadParameterDefinesFromDb()
        {
            if (GlobalData.SysCfg == null) return new List<ParameterDefineEntity>();
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            db.Connect();
            return db.Query<ParameterDefineEntity>("Sys_ParameterDefines", "1=1 ORDER BY SortOrder", null) ?? new List<ParameterDefineEntity>();
        }

        /// <summary>
        /// 更新或插入参数定义
        /// </summary>
        public void SaveParameterDefine(ParameterDefineEntity entity)
        {
            if (GlobalData.SysCfg == null) return;
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            db.Connect();
            if (entity.Id > 0) db.Update(entity);
            else {
                if (string.IsNullOrEmpty(entity.KeyId)) entity.KeyId = Guid.NewGuid().ToString();
                db.Insert(entity);
            }
        }

        /// <summary>
        /// 删除参数定义
        /// </summary>
        public void DeleteParameterDefine(ParameterDefineEntity entity)
        {
            if (GlobalData.SysCfg == null || entity.Id <= 0) return;
            using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            db.Connect();
            db.Delete(entity);
        }

        private void MigrateDevicesFromJson(SQLHelper db)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "devices.json");
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var devices = JsonSerializer.Deserialize<List<ConfigEntity>>(json);
                if (devices != null)
                {
                    foreach (var d in devices)
                    {
                        // 插入主设备表
                        var sysDev = new SysDeviceEntity
                        {
                            DeviceId = d.ID,
                            Name = d.Name,
                            Protocol = d.Protocol,
                            Description = "Migrated from JSON",
                            TimeSyncJson = JsonSerializer.Serialize(d.TimeSync)
                        };
                        db.Insert(sysDev);

                        // 插入参数表
                        if (d.CommParsams != null)
                        {
                            foreach (var p in d.CommParsams)
                            {
                                var sysParam = new SysDeviceParamEntity
                                {
                                    DeviceId = d.ID, // 使用业务 DeviceId 关联
                                    ParamName = p.Name,
                                    ParamValue = p.Value
                                };
                                db.Insert(sysParam);
                            }
                        }
                    }
                    LogHelper.Info($"[DataManager] 成功迁移 {devices.Count} 个设备到数据库。");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 设备数据迁移异常", ex);
                throw;
            }
        }

        private void MigrateGraphicsFromJson(SQLHelper db)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "GraphicDictionary.json");
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                var groups = JsonSerializer.Deserialize<List<SerializableGraphicGroup>>(json);
                if (groups != null)
                {
                    foreach (var g in groups)
                    {
                        // 插入组
                        var sysGroup = new SysGraphicGroupEntity { GroupName = g.Name };
                        db.Insert(sysGroup);
                        
                        // 获取刚插入的 ID (假设 GroupName 唯一，重新查询获取 ID)
                        // 注意：SQLHelper 如果没有返回 Insert ID 的功能，我们需要查一下
                        // 为简单起见，这里假设 Insert 成功后我们查询一下 ID
                        var dbGroup = db.Query<SysGraphicGroupEntity>("SysGraphicGroupEntity", "GroupName=@n", new MySql.Data.MySqlClient.MySqlParameter("@n", g.Name)).FirstOrDefault();
                        
                        if (dbGroup != null)
                        {
                            int sortIdx = 0;
                            foreach (var item in g.Items)
                            {
                                var sysItem = new SysGraphicItemEntity
                                {
                                    GroupId = dbGroup.Id,
                                    PathData = item.PathData,
                                    FillColor = item.FillColor,
                                    StrokeColor = item.StrokeColor,
                                    StrokeThickness = item.StrokeThickness,
                                    SortIndex = sortIdx++
                                };
                                db.Insert(sysItem);
                            }
                        }
                    }
                    LogHelper.Info($"[DataManager] 成功迁移 {groups.Count} 组图形到数据库。");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 图形数据迁移异常", ex);
                throw;
            }
        }

        private void CleanupOldLogTables()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;
                string logDb = GlobalData.SysCfg.LogDatabaseName;
                
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                db.Connect();
                
                var allTables = db.GetTableNames();
                int retentionMonths = GlobalData.DebugConfig?.LogRetentionMonths ?? 12;
                
                // 1. 清理物理日志文件 (.log)
                LogHelper.CleanupOldLogs(retentionMonths);

                // 2. 清理数据库分表
                var cutoffDate = DateTime.Now.AddMonths(-retentionMonths);
                int count = 0;

                foreach (var table in allTables)
                {
                    // 格式：PointLogs_yyyyMM
                    if (table.StartsWith("PointLogs_") && table.Length == 16)
                    {
                        string datePart = table.Substring(10); // yyyyMM
                        if (DateTime.TryParseExact(datePart, "yyyyMM", null, System.Globalization.DateTimeStyles.None, out DateTime tableDate))
                        {
                            if (tableDate < new DateTime(cutoffDate.Year, cutoffDate.Month, 1))
                            {
                                LogHelper.Info($"[DataManager] 清除过期日志表: {table}");
                                db.DropTableIfExists(table);
                                count++;
                            }
                        }
                    }
                }
                if (count > 0) LogHelper.Info($"[DataManager] 已清理 {count} 个过期日志分表。");
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] 清理过期日志表失败", ex);
            }
        }

        /// <summary>
        /// 加载图形字典 (优先从数据库加载)
        /// </summary>
        private void LoadGraphicDictionary()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;
                
                // 尝试从 DB 加载
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                try { db.Connect(); } catch { /* 连接失败则后续走文件兜底 */ }

                if (db.IsConnected)
                {
                    var groups = db.Query<SysGraphicGroupEntity>("SysGraphicGroupEntity", "1=1", null);
                    if (groups != null && groups.Count > 0)
                    {
                        GlobalData.GraphicDictionary?.Clear();
                        
                        // 一次性查出所有 Item，在内存中分组，减少 IO
                        var allItems = db.Query<SysGraphicItemEntity>("SysGraphicItemEntity", "1=1", null);
                        var itemLookup = allItems?.GroupBy(x => x.GroupId).ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortIndex).ToList());

                        foreach (var g in groups)
                        {
                            var iconList = new List<IconItem>();
                            if (itemLookup != null && itemLookup.TryGetValue(g.Id, out var dbItems))
                            {
                                foreach (var item in dbItems)
                                {
                                    try 
                                    {
                                        var geometry = Geometry.Parse(item.PathData);
                                        if (geometry.CanFreeze) geometry.Freeze();

                                        var stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.StrokeColor));
                                        if (stroke.CanFreeze) stroke.Freeze();

                                        var fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.FillColor));
                                        if (fill.CanFreeze) fill.Freeze();

                                        iconList.Add(new IconItem
                                        {
                                            Data = geometry,
                                            Stroke = stroke,
                                            Fill = fill,
                                            StrokeThickness = item.StrokeThickness
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        LogHelper.Error($"[DataManager] 解析图形项失败 Group:{g.GroupName} Id:{item.Id}", ex);
                                    }
                                }
                            }
                            GlobalData.GraphicDictionary[g.GroupName] = iconList;
                        }
                        LogHelper.Info($"[DataManager] 图形字典已从数据库加载. 包含 {groups.Count} 个分组。");
                        return; // 数据库加载成功，直接返回
                    }
                }
            }
            catch (Exception ex)
            {
                 LogHelper.Error("[DataManager] 从数据库加载图形失败，尝试回退到 JSON 文件", ex);
            }

            // Fallback: 文件加载 (原有逻辑)
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "GraphicDictionary.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var imported = JsonSerializer.Deserialize<List<SerializableGraphicGroup>>(json);
                if (imported != null)
                {
                    GlobalData.GraphicDictionary?.Clear();
                    foreach (var group in imported)
                    {
                        var itemList = new List<IconItem>();
                        foreach (var item in group.Items)
                        {
                            var geometry = Geometry.Parse(item.PathData);
                            if (geometry.CanFreeze) geometry.Freeze();

                            var stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.StrokeColor));
                            if (stroke.CanFreeze) stroke.Freeze();

                            var fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.FillColor));
                            if (fill.CanFreeze) fill.Freeze();

                            itemList.Add(new IconItem
                            {
                                Data = geometry,
                                Stroke = stroke,
                                Fill = fill,
                                StrokeThickness = item.StrokeThickness
                            });
                        }
                        GlobalData.GraphicDictionary[group.Name] = itemList;
                    }
                    var keys = string.Join(", ", GlobalData.GraphicDictionary.Keys);
                    LogHelper.Info($"[DataManager] 图形字典加载完成(JSON). 包含键: [{keys}]");
                }
            }
        }

        /// <summary>
        /// 加载设备列表 (优先从数据库加载)
        /// </summary>
        private void LoadDevices()
        {
            GlobalData.ListDveices = new List<ConfigEntity>();

            try 
            {
                 if (GlobalData.SysCfg != null)
                 {
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    try { db.Connect(); } catch { }

                    if (db.IsConnected)
                    {
                        var sysDevices = db.Query<SysDeviceEntity>("SysDeviceEntity", "1=1", null);
                        if (sysDevices != null && sysDevices.Count > 0)
                        {
                             var sysParams = db.Query<SysDeviceParamEntity>("SysDeviceParamEntity", "1=1", null);
                             var paramLookup = sysParams.ToLookup(p => p.DeviceId); // Key is Business ID

                             foreach (var sd in sysDevices)
                             {
                                 var config = new ConfigEntity
                                 {
                                     ID = sd.DeviceId,
                                     Name = sd.Name,
                                     Protocol = sd.Protocol,
                                     CommParsams = new List<CommParamEntity>()
                                 };
                                 
                                 // 反序列化 TimeSync
                                 if (!string.IsNullOrEmpty(sd.TimeSyncJson))
                                 {
                                    try {
                                        config.TimeSync = JsonSerializer.Deserialize<TimeSyncConfig>(sd.TimeSyncJson) ?? new TimeSyncConfig();
                                    } catch { config.TimeSync = new TimeSyncConfig(); }
                                 }

                                 // 加载参数
                                 if (paramLookup.Contains(sd.DeviceId))
                                 {
                                     foreach (var p in paramLookup[sd.DeviceId])
                                     {
                                         config.CommParsams.Add(new CommParamEntity
                                         {
                                              Name = p.ParamName,
                                              Value = p.ParamValue
                                         });
                                     }
                                 }
                                 GlobalData.ListDveices.Add(config);
                             }
                             LogHelper.Info($"[DataManager] 设备列表已从数据库加载: {GlobalData.ListDveices.Count} 台设备。");
                             return; // 成功返回
                        }
                    }
                 }
            }
            catch (Exception ex) 
            {
                LogHelper.Error("[DataManager] 从数据库加载设备列表失败，尝试回退到 JSON", ex);
            }

            // Fallback
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "devices.json");
            if (File.Exists(path))
            {
                try {
                    string json = File.ReadAllText(path);
                    GlobalData.ListDveices = JsonSerializer.Deserialize<List<ConfigEntity>>(json) ?? new List<ConfigEntity>();
                    LogHelper.Info($"[DataManager] 设备列表加载完成(JSON): {GlobalData.ListDveices.Count} 台设备。");
                } catch (Exception ex) {
                     LogHelper.Error("[DataManager] JSON 设备加载失败", ex);
                }
            }
        }

        #endregion

        #region 运行时点位管理

        /// <summary>
        /// 通用点位值更新入口
        /// </summary>
        /// <summary>
        /// 待更新任务表 (Key: 具体的点位配置对象)
        /// 直接使用对象引用作为 Key，彻底消除字符串拼接和内存分配。
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<object, bool> _pendingPointUpdates = new();
        private int _isBatchLoopRunning = 0;

        /// <summary>
        /// 通用点位值更新入口（改为批量缓冲模式）
        /// 通讯层接收到新数据后调用此方法，将变更压入队列
        /// </summary>
        /// <param name="targetObjId">目标对象ID (DoorId 或 PanelId)</param>
        /// <param name="bitConfigId">点位配置ID</param>
        /// <param name="targetType">目标类型 (门/面板)</param>
        /// <param name="value">新的布尔值</param>
        public void UpdatePointValue(string targetObjKeyId, string bitConfigKeyId, TargetType targetType, bool value)
        {
            if (!_isCacheBuilt) BuildBitCache();

            // 1. 查找缓存的对象引用，不使用字符串操作
            string key = $"{targetObjKeyId}_{bitConfigKeyId}";
            if (targetType == TargetType.Door)
            {
                if (_doorBitCache.TryGetValue(key, out var bit)) _pendingPointUpdates[bit] = value;
            }
            else
            {
                if (_panelBitCache.TryGetValue(key, out var bit)) _pendingPointUpdates[bit] = value;
            }

            // 2. 启动单循环
            if (System.Threading.Interlocked.CompareExchange(ref _isBatchLoopRunning, 1, 0) == 0)
            {
                StartBatchUpdateLoop();
            }
        }


        /// <summary>
        /// 启动批量更新循环
        /// 后台线程每100ms处理一次队列，将变更合并后调度到UI线程
        /// </summary>
        private void StartBatchUpdateLoop()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        // 1. 如果没有待更新点位，或者用户正在拖动，进入深度休眠
                        if (_pendingPointUpdates.IsEmpty || IsUIInteractionActive)
                        {
                            await Task.Delay(300); // 深度休眠，降低单核 CPU 损耗
                            continue;
                        }

                        // 减少快照频率，将原本的每秒10次检查降低为每秒4次，这对监控系统已足够
                        await Task.Delay(250); 

                        // 2. 快速摘取点位变化并清空
                        var updates = _pendingPointUpdates.ToArray();
                        _pendingPointUpdates.Clear();

                        var dirtyDoors = new HashSet<DoorModel>();

                        foreach (var update in updates)
                        {
                            if (update.Key is DoorBitConfig dbic)
                            {
                                if (dbic.BitValue != update.Value)
                                {
                                    dbic.BitValue = update.Value;
                                    // 根据注入的父级 ID 瞬间找到关联的门
                                    if (!string.IsNullOrEmpty(dbic.ParentDoorKeyId))
                                    {
                                        if (_doorCache.TryGetValue(dbic.ParentDoorKeyId, out var door))
                                        {
                                            dirtyDoors.Add(door);
                                        }
                                    }
                                }
                            }
                            else if (update.Key is PanelBitConfig pbic)
                            {
                                // 面板位由于数量极少（240个），直接更新属性即可，负载极低
                                pbic.BitValue = update.Value;
                            }
                        }

                        // 3. 只有发生变化的门（通常不到 5 个）才进入视觉裁决计算
                        if (dirtyDoors.Count == 0) continue;

                        // 3. 计算视觉指纹变化
                        var finalUpdates = new List<(DoorModel door, VisualStateResult result)>();
                        foreach (var door in dirtyDoors)
                        {
                            var res = CalculateDoorVisualState(door);
                            // 使用 DoorModel 中实际存在的 LastVisualFingerprint (Tuple)
                            var newFingerprint = (res.HeaderId, res.ImageId, res.BottomId, 0, 0);
                            if (door.LastVisualFingerprint != newFingerprint)
                            {
                                door.LastVisualFingerprint = newFingerprint;
                                finalUpdates.Add((door, res));
                            }
                        }

                        // 4. 集中推送 UI
                        if (finalUpdates.Count > 0)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                foreach (var item in finalUpdates)
                                {
                                    ApplyDoorVisualState(item.door, item.result);
                                }
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }

                        await Task.Delay(100); // 严格控制频率 10Hz
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DataManager] Batch loop error: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }
            });
        }

        /// <summary>
        /// 为所有运行时点位构建快速索引
        /// </summary>
        public void BuildBitCache()
        {
            _doorBitCache.Clear();
            _panelBitCache.Clear();
            _doorCache.Clear();

            // 1. 为门点位构建索引
            if (GlobalData.MainVm != null)
            {
                foreach (var stationVm in GlobalData.MainVm.Stations)
                {
                    var st = stationVm.Station;
                    foreach (var group in st.DoorGroups)
                    {
                        foreach (var door in group.Doors)
                        {
                            _doorCache[door.KeyId] = door;
                            foreach (var bit in door.Bits)
                            {
                                string key = $"{door.KeyId}_{bit.KeyId}";
                                _doorBitCache[key] = bit;
                                // 赋予父级引用，解决后台查找门的性能瓶颈
                                bit.ParentDoorKeyId = door.KeyId; // Store parent door key
                            }
                        }
                    }
                }
            }

            // 2. 为面板点位构建索引
            if (GlobalData.MainVm != null)
            {
                foreach (var stationVm in GlobalData.MainVm.Stations)
                {
                    var st = stationVm.Station;
                    foreach (var pg in st.PanelGroups)
                    {
                        foreach (var p in pg.Panels)
                        {
                            foreach (var bit in p.BitList)
                            {
                                string key = $"{p.KeyId}_{bit.KeyId}";
                                _panelBitCache[key] = bit;
                            }
                        }
                    }
                }
            }

            _isCacheBuilt = true;
            LogHelper.Info($"[DataManager] 点位缓存构建完成。门点位={_doorBitCache.Count}, 门数量={_doorCache.Count}");
        }

        #endregion

        #region 业务逻辑裁决 (Visual Adjudication)

        /// <summary>
        /// 后台裁决结果载体 (采用固定槽位而非列表，极大降低 UI 渲染开销)
        /// </summary>
        /// <summary>
        /// 后台视觉裁决结果载体
        /// </summary>
        public struct VisualStateResult
        {
            public int HeaderId;
            public int ImageId;
            public int BottomId;
            public Brush HeaderBrush; // 顶部状态条背景色
            public Brush BottomBrush; // 底部状态条背景色
            
            // 使用 ObservableCollection 以支持 WPF 通知属性，确保多层矢量图即时刷新
            public ObservableCollection<IconItem> IconsHeader;
            public ObservableCollection<IconItem> IconsMiddle;
            public ObservableCollection<IconItem> IconsBottom;
        }

        /// <summary>
        /// (后台线程执行) 计算门的视觉状态，不触动 UI 元素
        /// </summary>
        private VisualStateResult CalculateDoorVisualState(DoorModel door)
        {
            var res = new VisualStateResult();
            if (door?.Bits == null) return res;

            // 1. 高性能筛选激活点位 (非 LINQ)
            DoorBitConfig? bestHeader = null;
            DoorBitConfig? bestImage = null;
            DoorBitConfig? bestBottom = null;

            foreach (var b in door.Bits)
            {
                if (!b.BitValue) continue;

                if (b.HeaderPriority > 0 && (bestHeader == null || b.HeaderPriority > bestHeader.HeaderPriority))
                    bestHeader = b;

                if (!string.IsNullOrEmpty(b.GraphicName))
                {
                    if (bestImage == null || b.ImagePriority > bestImage.ImagePriority || (b.ImagePriority == bestImage.ImagePriority && b.BitId > bestImage.BitId))
                        bestImage = b;
                }

                if (b.BottomPriority > 0 && (bestBottom == null || b.BottomPriority > bestBottom.BottomPriority))
                    bestBottom = b;
            }

            res.HeaderId = bestHeader?.BitId ?? 0;
            res.ImageId = bestImage?.BitId ?? 0;
            res.BottomId = bestBottom?.BitId ?? 0;
            res.HeaderBrush = bestHeader?.HeaderColor ?? System.Windows.Media.Brushes.Gray;
            res.BottomBrush = bestBottom?.BottomColor ?? System.Windows.Media.Brushes.Transparent;

            // 1. 颜色冻结
            if (res.HeaderBrush.CanFreeze) res.HeaderBrush.Freeze();
            if (res.BottomBrush.CanFreeze) res.BottomBrush.Freeze();

            // 2. 核心图标投放：改为多图层提取模式，支持数据库中定义的“每一个图层”
            string hName = bestHeader?.GraphicName;
            Brush hFill = bestHeader?.GraphicColor;
            string mName = bestImage?.GraphicName ?? "关门";
            Brush mFill = bestImage?.GraphicColor ?? Brushes.LightGray;
            string bName = bestBottom?.GraphicName;
            Brush bFill = bestBottom?.GraphicColor;

            res.IconsHeader = GetIconItems(hName, hFill);
            res.IconsMiddle = GetIconItems(mName, mFill);
            res.IconsBottom = GetIconItems(bName, bFill);
            
            return res;
        }

        /// <summary>
        /// (核心函数) 从图形字典中提取完整矢量图层组（质量图）
        /// </summary>
        public static ObservableCollection<IconItem> GetIconItems(string name, Brush fill)
        {
            var result = new ObservableCollection<IconItem>();
            if (string.IsNullOrEmpty(name)) return result;
            
            if (GlobalData.GraphicDictionary != null && GlobalData.GraphicDictionary.TryGetValue(name, out var templates))
            {
                // 关键修复：如果模板包含多于一个图层，说明是复杂的“质量图”，具备精细的配色（如拉手、边框等）。
                // 此时不应盲目使用单一的状态覆盖色（fill），否则会导致“彩色图变成单色块”。
                bool isQualityMap = templates.Count > 1;

                foreach (var original in templates)
                {
                    // 返回克隆对象，确保每个门的图标实例独立
                    var item = original.Clone();
                    
                    // 仅在单图层模式（普通图标）下，且传入了有效的覆盖色时，才进行染色。
                    // 这样可以保留高质量质量图原有的多层色彩。
                    if (!isQualityMap && fill != null && fill != Brushes.Transparent && fill != Brushes.LightGray)
                    {
                        if (fill.CanFreeze) fill.Freeze();
                        item.Fill = fill;
                    }
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// (UI 线程执行) 仅仅进行最后的属性赋值
        /// </summary>
        private void ApplyDoorVisualState(DoorModel door, VisualStateResult res)
        {
            if (door?.Visual == null) return;

            door.Visual.HeaderBackground = res.HeaderBrush;
            door.Visual.BottomBackground = res.BottomBrush;
            
            // 兼容性保留
            door.Visual.IconTop = res.IconsHeader?.FirstOrDefault();
            door.Visual.IconMid = res.IconsMiddle?.FirstOrDefault();
            door.Visual.IconBot = res.IconsBottom?.FirstOrDefault();

            // 核心修复：更新 IconItems 集合。
            // 对应空间库 DoorControl 的 Row 1 (中间区域) 绑定。
            // 只放入 IconsMiddle 图层，解决多个槽位重叠导致的"错位/混乱"问题。
            door.Visual.IconItems = res.IconsMiddle ?? System.Linq.Enumerable.Empty<ControlLibrary.Models.IconItem>();

            door.Visual.HeaderText = door.DoorName; // 保持文字更新
        }

        /// <summary>
        /// (废弃/保留兼容) 基于优先级裁决门的所有视觉要素
        /// </summary>
        [Obsolete("请使用 CalculateDoorVisualState + ApplyDoorVisualState")]
        public void AdjudicateDoorVisual(DoorModel door)
        {
             var res = CalculateDoorVisualState(door);
             ApplyDoorVisualState(door, res);
        }

        #endregion
        #region 操作日志记录

        /// <summary>
        /// 记录用户关键操作 (登录/退出/控制)
        /// 会同时写入文本日志和数据库 Sys_OperationLogs 表
        /// </summary>
        public void LogOperation(string operationType, string description, string result = "Success")
        {
            string username = GlobalData.CurrentUser?.Username ?? "System";
            
            // 1. 文本日志
            LogHelper.Info($"[OP] User:{username} Type:{operationType} Desc:{description} Res:{result}");
            
            // 2. 数据库日志
            Task.Run(() => {
                try
                {
                    if (GlobalData.SysCfg == null) return;
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();
                    if (db.IsConnected)
                    {
                        var log = new OperationLogEntity
                        {
                            LogTime = DateTime.Now,
                            Username = username,
                            OperationType = operationType,
                            Description = description,
                            Result = result
                        };
                        db.Insert(log);
                    }
                }
                catch (Exception ex)
                {
                    // 数据库写失败不要崩，记录错误即可
                    LogHelper.Error("[LogOperation] Failed to write to DB", ex);
                }
            });
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放后台任务资源（取消 CancellationToken）
        /// 应在 MainWindow.Closed 事件中调用
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _backgroundCts.Cancel();
            _backgroundCts.Dispose();
        }

        #endregion

    }
}
