using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DoorMonitorSystem.Assets.Helper
{
    public static class LogHelper
    {
        private static readonly string BaseLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private static readonly object _lock = new object();

        private static string _lastMessage = "";
        private static string _lastLevel = "";
        private static int _repeatCount = 0;
        private static DateTime _firstOccurTime;
        private static DateTime _lastOccurTime;
        private static readonly Timer _flushTimer;

        static LogHelper()
        {
            // 初始化定时器，用于在日志停止刷新一段时间后自动写入汇总
            _flushTimer = new Timer(OnFlushTimer, null, Timeout.Infinite, Timeout.Infinite);
        }

        private static void OnFlushTimer(object? state)
        {
            lock (_lock)
            {
                FlushRepeatedLog();
            }
        }

        /// <summary>
        /// 写入日志（带防抖去重）
        /// </summary>
        public static void WriteLog(string message, string level = "INFO")
        {
            try
            {
                lock (_lock)
                {
                    // 去重逻辑：如果消息内容和等级与上一条完全一致
                    if (message == _lastMessage && level == _lastLevel)
                    {
                        _repeatCount++;
                        _lastOccurTime = DateTime.Now;
                        // 重置定时器 (5秒后无新日志则强制刷新)
                        _flushTimer.Change(5000, Timeout.Infinite);
                        return;
                    }

                    // 如果是新消息，先结算上一条的重复记录
                    FlushRepeatedLog();

                    // 记录新消息
                    _lastMessage = message;
                    _lastLevel = level;
                    _repeatCount = 1;
                    _firstOccurTime = DateTime.Now;
                    _lastOccurTime = DateTime.Now;
                    
                    WriteToFile(message, level, DateTime.Now);
                    
                    // 同样启动定时器，防止只有一条消息时无法结算
                    _flushTimer.Change(5000, Timeout.Infinite);
                }
            }
            catch
            {
                // Ignore
            }
        }

        private static void FlushRepeatedLog()
        {
            if (_repeatCount > 1)
            {
                TimeSpan duration = _lastOccurTime - _firstOccurTime;
                string summary = $"[LOG_SYSTEM] Above message repeated {_repeatCount - 1} more times. " +
                                 $"Duration: {duration.TotalSeconds:F2}s. Last occurred at {_lastOccurTime:HH:mm:ss.fff}";
                WriteToFile(summary, "SUMMARY", DateTime.Now);
            }
            
            // 重置状态
            _repeatCount = 0;
            _lastMessage = "";
            _lastLevel = "";
            _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static void WriteToFile(string message, string level, DateTime time)
        {
            try
            {
                string dirPath = Path.Combine(BaseLogDir, time.ToString("yyyy"), time.ToString("MM"), time.ToString("dd"));
                string filePath = Path.Combine(dirPath, $"{time.ToString("HH")}.log");

                if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

                string logContent = $"[{time:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(filePath, logContent, Encoding.UTF8);
            }
            catch { }
        }

        public static void Info(string message) => WriteLog(message, "INFO");
        public static void Error(string message) => WriteLog(message, "ERROR");
        public static void Error(string message, Exception ex) => WriteLog($"{message} -> {ex}", "ERROR");

        /// <summary>
        /// 清理超过1年的日志文件
        /// </summary>
        public static void CleanupOldLogs()
        {
            Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(BaseLogDir)) return;

                    var cutoffDate = DateTime.Now.AddIndexedYears(-1); // 1年前

                    // 遍历年份文件夹
                    foreach (var yearDir in Directory.GetDirectories(BaseLogDir))
                    {
                        var yearName = Path.GetFileName(yearDir);
                        if (int.TryParse(yearName, out int year))
                        {
                            // 如果年份明显小于去年，直接删除整年
                            if (year < cutoffDate.Year)
                            {
                                try { Directory.Delete(yearDir, true); } catch { }
                                continue;
                            }

                            // 如果是同一年，或者去年的，深入检查月份
                            foreach (var monthDir in Directory.GetDirectories(yearDir))
                            {
                                var monthName = Path.GetFileName(monthDir);
                                if (int.TryParse(monthName, out int month))
                                {
                                    // 构造当前文件夹代表的时间 (大概)
                                    var dirDate = new DateTime(year, month, 1);
                                    
                                    // 简单的逻辑：如果这个月的最后一天都比截止日期老，就删除
                                    if (dirDate.AddMonths(1) < cutoffDate)
                                    {
                                         try { Directory.Delete(monthDir, true); } catch { }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Log Cleanup Failed: {ex.Message}");
                }
            });
        }
    }
    
    // 扩展方法
    public static class DateTimeExtensions
    {
        public static DateTime AddIndexedYears(this DateTime dt, int years)
        {
            return dt.AddYears(years);
        }
    }
}
