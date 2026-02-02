using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using DoorMonitorSystem.Models;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 日志持久化服务 (单例模式)
    /// 核心功能：
    /// 1. 日志变位评估与去重。
    /// 2. 异步高性能批量入库 (支持分表存储)。
    /// 3. 数据库故障自动切换脱机存储 (CSV)。
    /// 4. 数据库恢复后自动寻回并重传。
    /// </summary>
    public class LogService : IDisposable
    {
        // 懒汉式单例，确保全局只有一个日志中心
        private static readonly Lazy<LogService> _instance = new(() => new LogService());
        public static LogService Instance => _instance.Value;

        // 日志缓冲队列：通讯线程只管往里塞，后台线程负责异步写库，不阻塞采集。
        private readonly BlockingCollection<PointLogEntity> _logQueue = new();
        
        // 缓存点位的最后一次状态 (改为 object 以支持模拟量)
        private readonly ConcurrentDictionary<int, object> _lastValues = new();
        
        private bool _isLogConsumerRunning = false;
        private Task? _logConsumerTask;
        private bool _isRunning = true;

        private LogService()
        {
            // 初始化即启动后台消费线程
            StartLogConsumer();
        }

        /// <summary>
        /// 处理点位日志逻辑：评估当前值是否满足“变位触发”或“指定状态触发”的记录条件。
        /// </summary>
        /// <param name="p">点位配置实体</param>
        /// <param name="bitValue">当前读取到的位值 (用于开关量判定)</param>
        /// <param name="rawValue">当前读取到的原始值 (用于模拟量判定)</param>
        public void ProcessLogging(DevicePointConfigEntity p, bool bitValue, object rawValue)
        {
            // 0. 权限检查
            if (p == null || !p.IsLogEnabled) return;

            string dType = p.DataType?.ToLower() ?? "";
            bool isDigital = dType.Contains("bool") || dType.Contains("bit"); // 归一化判定
            object currentValue = isDigital ? (object)bitValue : rawValue;
            
            // 1. 获取旧值
            bool isFirstTime = !_lastValues.TryGetValue(p.Id, out object lastValue);

            // 2. 变位判定逻辑
            bool isChanged = false;
            if (isFirstTime)
            {
                isChanged = true;
            }
            else
            {
                if (isDigital)
                {
                    isChanged = (bool)lastValue != bitValue;
                }
                else
                {
                    // 模拟量判定 (支持死区)
                    if (p.LogDeadband.HasValue && p.LogDeadband.Value > 0)
                    {
                        try 
                        {
                            double d1 = Convert.ToDouble(lastValue);
                            double d2 = Convert.ToDouble(rawValue);
                            if (Math.Abs(d2 - d1) >= p.LogDeadband.Value) isChanged = true;
                        }
                        catch 
                        {
                            // 转换失败则退化为字符串比对
                            isChanged = lastValue.ToString() != rawValue.ToString();
                        }
                    }
                    else
                    {
                        // 无死区，直接比对字符串 (避免浮点微小差异，也可直接 Equals)
                        isChanged = lastValue.ToString() != rawValue.ToString();
                    }
                }
            }

            // 更新缓存 (如果变了)
            if (isChanged) _lastValues[p.Id] = currentValue;
            else return; // 没变就不记录

            // 3. 评估触发/记录内容
            // 3.1 模拟量总是记录 (只要变了)，且检查报警
            // 3.2 开关量需检查 LogTriggerState
            
            bool shouldLog = false;
            int logType = p.LogTypeId <= 0 ? 1 : p.LogTypeId; // 默认使用配置的类型，如果是0则修正为1

            if (!isDigital)
            {
                // --- 模拟量逻辑 ---
                shouldLog = true; // 只要变了且过了死区就记录

                // 检查高低限报警
                try 
                {
                    double val = Convert.ToDouble(rawValue);
                    if (p.HighLimit.HasValue && val >= p.HighLimit.Value) 
                    {
                         logType = 2; // 强制报警类型
                    }
                    else if (p.LowLimit.HasValue && val <= p.LowLimit.Value)
                    {
                         logType = 2; // 强制报警类型
                    }
                } 
                catch {}
            }
            else
            {
                // --- 开关量逻辑 ---
                if (p.LogTriggerState == 2) shouldLog = true;
                else if (p.LogTriggerState == 1 && bitValue) shouldLog = true;
                else if (p.LogTriggerState == 0 && !bitValue) shouldLog = true;
            }

            if (shouldLog)
            {
                // 增强默认消息内容：如果未配置模板，自动补齐分类前缀 [分类名] 描述
                string logMsg = string.IsNullOrWhiteSpace(p.LogMessage) ? 
                    (!string.IsNullOrEmpty(p.Category) ? $"[{p.Category}] {p.Description}" : p.Description) : 
                    p.LogMessage;

                string valText = "";

                if (isDigital)
                {
                    valText = bitValue ? 
                        (!string.IsNullOrWhiteSpace(p.State1Desc) ? p.State1Desc : "ON") : 
                        (!string.IsNullOrWhiteSpace(p.State0Desc) ? p.State0Desc : "OFF");
                }
                else
                {
                     valText = rawValue?.ToString() ?? "";
                }

                // 支持模板替换：{Value} {Category} {Description} {Address}
                logMsg = logMsg?.Replace("{Value}", valText)
                                .Replace("{Category}", p.Category ?? "")
                                .Replace("{Description}", p.Description ?? "")
                                .Replace("{Address}", p.Address ?? "") ?? "";

                var entity = new PointLogEntity
                {
                    PointID = p.Id,
                    DeviceID = p.SourceDeviceId,
                    Address = isDigital ? $"{p.Address}.{p.BitIndex}" : p.Address,
                    Val = bitValue ? 1 : 0, 
                    ValText = valText,     
                    LogType = logType,     
                    Message = logMsg,
                    Category = p.Category, 
                    UserName = GlobalData.CurrentUser?.Username ?? "System",
                    LogTime = DateTime.Now,
                    IsAnalog = !isDigital 
                };

                _logQueue.Add(entity);
            }
        }

        /// <summary>
        /// 添加自定义业务日志（非点位触发）
        /// </summary>
        /// <param name="message">日志内容</param>
        /// <param name="category">日志分类</param>
        public void AddCustomLog(string message, string category = "系统")
        {
            var entity = new PointLogEntity
            {
                PointID = -1,    // 非物理点位标识
                DeviceID = -1,   // 系统级事件
                LogType = 1,     // 状态/信息类型
                Message = message,
                ValText = "操作",
                Category = category,
                UserName = GlobalData.CurrentUser?.Username ?? "System",
                LogTime = DateTime.Now
            };
            //输出到数据库
            // _logQueue.Add(entity);

            // 同步输出到文本日志作为备用
            LogHelper.Info($"[USER_OP] {category}: {message}");
        }

        /// <summary>
        /// 启动日志消费者线程：
        /// 规则：满足 50 条或等待超过 3 秒，执行一次批量写入。
        /// </summary>
        private void StartLogConsumer()
        {
            if (_isLogConsumerRunning) return;
            _isLogConsumerRunning = true;

            // 启动脱机日志自动寻回/恢复任务 (每30秒跑一次)
            _ = StartOfflineRecoveryLoop();

            // 检查并升级当前月表的Schema (确保毫秒精度)
            _ = CheckAndUpgradeSchemaAsync();

            _logConsumerTask = Task.Run(async () =>
            {
                var batch = new List<PointLogEntity>();
                DateTime lastFlushTime = DateTime.Now;

                while (_isRunning || !_logQueue.IsCompleted)
                {
                    try
                    {
                        // 1. 尝试从队列获取一条，阻塞 100ms
                        if (_logQueue.TryTake(out var entity, 100))
                        {
                            batch.Add(entity);
                        }

                        // 2. 检查是否满足冲刷(Flush)条件
                        var secondsSinceLastFlush = (DateTime.Now - lastFlushTime).TotalSeconds;

                        // 同步策略：50条起步 或 3秒保底
                        if (batch.Count > 0 && (batch.Count >= 50 || secondsSinceLastFlush >= 3))
                        {
                            await WriteLogBatchAsync(batch);
                            batch.Clear();
                            lastFlushTime = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LogService] 消费异常: {ex.Message}");
                        await Task.Delay(1000);
                    }
                }

                // 退出循环时(关机)，强制把桶里剩下的水倒完
                if (batch.Count > 0)
                {
                    await WriteLogBatchAsync(batch, false);
                }
            });
        }

        /// <summary>
        /// 循环检测本地脱机文件夹，若数据库恢复则自动回传同步
        /// </summary>
        private async Task StartOfflineRecoveryLoop()
        {
            while (_isRunning)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30)); // 每30秒尝试一次
                    await SyncOfflineLogsAsync();
                }
                catch { }
            }
        }

        /// <summary>
        /// 执行批量数据写入逻辑 (核心持久化方法)
        /// </summary>
        /// <param name="batch">日志批次列表</param>
        /// <param name="allowFailover">若写入失败，是否开启本地脱机缓存保护</param>
        private async Task WriteLogBatchAsync(List<PointLogEntity> batch, bool allowFailover = true)
        {
            if (GlobalData.SysCfg == null || batch.Count == 0) return;
            string logDb = GlobalData.SysCfg.LogDatabaseName;

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, logDb);
                db.Connect();

                await Task.Run(() =>
                {
                    try
                    {
                        db.BeginTransaction();
                        foreach (var entity in batch)
                        {
                            // 动态路由表名：PointLogs_202401 或 AnalogLogs_202401
                            string prefix = entity.IsAnalog ? "AnalogLogs" : "PointLogs";
                            string tableName = $"{prefix}_{entity.LogTime:yyyyMM}";
                            db.Insert(entity, tableName);
                        }
                        db.CommitTransaction();
                    }
                    catch (Exception ex)
                    {
                        db.RollbackTransaction();
                        // 保护：如果判定为表不存在，则触发分表创建逻辑并重试一次
                        if (ex.Message.Contains("Table") && ex.Message.Contains("doesn't exist"))
                        {
                            // 确保批处理中涉及的所有表都已创建
                            var tables = batch.Select(x => new { Prefix = x.IsAnalog ? "AnalogLogs" : "PointLogs", Time = x.LogTime })
                                             .GroupBy(x => $"{x.Prefix}_{x.Time:yyyyMM}")
                                             .Select(g => g.First());
                            
                            foreach (var t in tables)
                            {
                                EnsureShardTableExists(db, t.Time, t.Prefix);
                            }

                            db.BeginTransaction();
                            foreach (var entity in batch)
                            {
                                string TablePrefix = entity.IsAnalog ? "AnalogLogs" : "PointLogs";
                                string tableName = $"{TablePrefix}_{entity.LogTime:yyyyMM}";
                                db.Insert(entity, tableName);
                            }
                            db.CommitTransaction();
                        }
                        // 保护: 如果是新增字段导致的错误 (如 ValText)
                        else if (ex.Message.Contains("Unknown column")) 
                        {
                             // 确保批处理中涉及的所有表都进行补丁
                             var tables = batch.Select(x => new { Prefix = x.IsAnalog ? "AnalogLogs" : "PointLogs", Time = x.LogTime })
                                              .GroupBy(x => $"{x.Prefix}_{x.Time:yyyyMM}")
                                              .Select(g => g.First());

                             foreach (var t in tables)
                             {
                                 string tableName = $"{t.Prefix}_{t.Time:yyyyMM}";
                                 try { db.ExecuteNonQuery($"ALTER TABLE `{tableName}` ADD COLUMN ValText VARCHAR(50);"); } catch { }
                                 try { db.ExecuteNonQuery($"ALTER TABLE `{tableName}` ADD COLUMN Category VARCHAR(50);"); } catch { }
                                 try { db.ExecuteNonQuery($"ALTER TABLE `{tableName}` ADD COLUMN UserName VARCHAR(50);"); } catch { }
                             }

                             db.BeginTransaction();
                             foreach (var entity in batch)
                             {
                                 string TablePrefix = entity.IsAnalog ? "AnalogLogs" : "PointLogs";
                                 string tableName = $"{TablePrefix}_{entity.LogTime:yyyyMM}";
                                 db.Insert(entity, tableName);
                             }
                             db.CommitTransaction();
                        }
                        else throw;
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.Error($"[LogService] 数据库批量写入失败: {ex.Message}");
                // 链路断开：将数据保存为本地 CSV，确保护现场数据不丢失
                if (allowFailover)
                {
                    SaveToOfflineStorage(batch);
                }
                else
                {
                    throw; // 同步模式下由外层捕获异常，不会删除本地文件
                }
            }
        }

        /// <summary>
        /// 脱机急救存储：将内存中积压的日志保存为本地 CSV 文件
        /// </summary>
        private void SaveToOfflineStorage(List<PointLogEntity> batch)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "OfflineStorage");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // 采用精确到毫秒的文件名，防止冲突
                string fileName = $"Offline_{DateTime.Now:yyyyMMdd_HHmmss_fff}.csv";
                string path = Path.Combine(dir, fileName);

                // 利用 MiniExcel 瞬间生成文件
                MiniExcelLibs.MiniExcel.SaveAs(path, batch.Select(x => new {
                    x.PointID,
                    x.DeviceID,
                    x.Address, 
                    x.Val,
                    x.ValText,
                    x.LogType,
                    x.Message,
                    x.Category,
                    x.UserName,
                    x.LogTime,
                    x.IsAnalog
                }));

                LogHelper.Info($"[LogService] 数据库脱机，已暂存 {batch.Count} 条记录至本地: {fileName}");
            }
            catch (Exception ex)
            {
                LogHelper.Error("[LogService] 脱机落盘失败，数据有丢失风险！", ex);
            }
        }

        /// <summary>
        /// 自动同步逻辑：一旦连上网，按时间顺序把积压的脱机包推回数据库
        /// </summary>
        private async Task SyncOfflineLogsAsync()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "OfflineStorage");
            if (!Directory.Exists(dir)) return;

            // 获取所有以 Offline_ 开头的 CSV 文件并排序
            var files = Directory.GetFiles(dir, "Offline_*.csv").OrderBy(f => f).ToList();
            if (files.Count == 0) return;

            // 预连测试，避免不必要的报错
            try
            {
                if (GlobalData.SysCfg == null) return;
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.LogDatabaseName);
                db.Connect();
            }
            catch
            {
                return; // 依然连不上直接退出，等待下一个周期
            }

            LogHelper.Info($"[LogService] 数据库连接已恢复，正在回传 {files.Count} 个脱机暂存包...");

            foreach (var file in files)
            {
                try
                {
                    // 1. 读取本地数据并转回实体
                    var rows = MiniExcelLibs.MiniExcel.Query<PointLogEntity>(file).ToList();
                    if (rows.Count > 0)
                    {
                        // 2. 尝试写入数据库 (此处必须设置 allowFailover=false，防止失败又在本地生成新文件导致死循环)
                        await WriteLogBatchAsync(rows, false);
                    }
                    // 3. 写入成功，删除本地媒介
                    File.Delete(file);
                }
                catch
                {
                    // 任一文件同步失败则终止本次循环，避免频繁重试报错
                    break;
                }
            }
        }

        /// <summary>
        /// 确保日志分表（如 PointLogs_202401 或 AnalogLogs_202401）在数据库中存在
        /// </summary>
        private void EnsureShardTableExists(SQLHelper db, DateTime date, string prefix = "PointLogs")
        {
            try
            {
                string tableName = $"{prefix}_{date:yyyyMM}";
                db.CreateTableFromModel<PointLogEntity>(tableName);
            }
            catch { }
        }

        /// <summary>
        /// 检查当前月表的 Schema，确保 LogTime 字段具有毫秒精度
        /// </summary>
        private async Task CheckAndUpgradeSchemaAsync()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;
                await Task.Run(() =>
                {
                    try
                    {
                        using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.LogDatabaseName);
                        db.Connect();

                        string[] prefixes = { "PointLogs", "AnalogLogs" };
                        foreach (var pfx in prefixes)
                        {
                            string tableName = $"{pfx}_{DateTime.Now:yyyyMM}";
                            if (db.TableExists(tableName))
                            {
                                var dt = db.ExecuteQuery($"SHOW COLUMNS FROM `{tableName}` WHERE Field='LogTime'");
                                if (dt.Rows.Count > 0)
                                {
                                    string type = dt.Rows[0]["Type"].ToString()?.ToLower() ?? "";
                                    // 如果当前不仅是 datetime(3) (例如 datetime, 或 datetime(0))
                                    if (!type.Contains("datetime(3)"))
                                    {
                                        // 升级字段精度
                                        db.ExecuteNonQuery($"ALTER TABLE `{tableName}` MODIFY COLUMN LogTime DATETIME(3)");
                                        LogHelper.Info($"[LogService] 已自动升级表 {tableName} schema: LogTime -> DATETIME(3)");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error("[LogService] Schema 检查/升级失败", ex);
                    }
                });
            }
            catch { }
        }

        /// <summary>
        /// 优雅关闭：确保关机前 2 秒内所有内存数据都能及时落盘
        /// </summary>
        public void Dispose()
        {
            _isRunning = false;
            try
            {
                _logQueue.CompleteAdding();
                _logConsumerTask?.Wait(2000); // 最多等 2 秒，兼顾系统响应和数据完整性
            }
            catch { }
        }
    }
}
