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
        
        // 缓存点位的最后一次状态，用于“变位触发”判断（只有不同于上次才记录）。
        private readonly ConcurrentDictionary<int, bool> _lastValues = new();
        
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
        /// <param name="currentValue">当前读取到的原始位值 (True/False)</param>
        public void ProcessLogging(DevicePointConfigEntity p, bool currentValue)
        {
            // 0. 权限检查：如果点位对象为空，或者未在配置中勾选“开启日志记录”，则不处理。
            if (p == null || !p.IsLogEnabled) return;

            // 1. 获取旧值并更新 (TryGetValue 返回 False 表示系统刚启动，此为第一次赋值)
            bool isFirstTime = !_lastValues.TryGetValue(p.Id, out bool lastValue);
            _lastValues[p.Id] = currentValue;

            // 2. 如果值没变，则无需记录
            if (!isFirstTime && currentValue == lastValue) return;

            // 3. 评估触发逻辑 (LogTriggerState 定义：0=False触发, 1=True触发, 2=两者均触发/变位触发)
            bool shouldLog = false;
            if (p.LogTriggerState == 2) shouldLog = true;
            else if (p.LogTriggerState == 1 && currentValue) shouldLog = true;
            else if (p.LogTriggerState == 0 && !currentValue) shouldLog = true;

            if (shouldLog)
            {
                // 解析消息模板 (支持 {Value} 占位符替换为 ON/OFF)
                string logMsg = string.IsNullOrWhiteSpace(p.LogMessage) ? p.Description : p.LogMessage;
                logMsg = logMsg?.Replace("{Value}", currentValue ? "ON" : "OFF") ?? "";

                var entity = new PointLogEntity
                {
                    PointID = p.Id,
                    DeviceID = p.SourceDeviceId,
                    Address = $"{p.Address}.{p.BitIndex}",
                    Val = currentValue ? 1 : 0,
                    LogType = p.LogTypeId, // 1=报警, 其他=状态
                    Message = logMsg,
                    LogTime = DateTime.Now
                };

                // 将日志抛入队列，由后台任务异步持久化
                _logQueue.Add(entity);
            }
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
                            // 动态路由表名：PointLogs_202401
                            string tableName = $"PointLogs_{entity.LogTime:yyyyMM}";
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
                            var firstEntity = batch[0];
                            EnsureShardTableExists(db, firstEntity.LogTime);

                            db.BeginTransaction();
                            foreach (var entity in batch)
                            {
                                string tableName = $"PointLogs_{entity.LogTime:yyyyMM}";
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
                    x.LogType,
                    x.Message,
                    x.LogTime
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
        /// 确保日志分表（如 PointLogs_202401）在数据库中存在
        /// </summary>
        private void EnsureShardTableExists(SQLHelper db, DateTime date)
        {
            try
            {
                string tableName = $"PointLogs_{date:yyyyMM}";
                db.CreateTableFromModel<PointLogEntity>(tableName);
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
