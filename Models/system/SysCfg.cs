
namespace DoorMonitorSystem.Models.system
{
    public class SysCfg
    {
        public  string ServerAddress { get; set; } = "";
        public  string UserName { get; set; } = "";
        public  string UserPassword { get; set; } = "";
        public  string DatabaseName { get; set; } = "";
        /// <summary>
        /// 日志数据库名 (固定不可修改)
        /// </summary>
        public string LogDatabaseName => string.IsNullOrWhiteSpace(DatabaseName) ? "DoorMonitorLogs" : $"{DatabaseName}_Logs";
        
        /// <summary>
        /// 启动屏幕索引 (0=主屏, 1=副屏...)
        /// </summary>
        public int MonitorIndex { get; set; } = 0;

        /// <summary>
        /// 站台/监测点名称 (显示在主界面)
        /// </summary>
        public string StationName { get; set; } = "XX监测站";
    }
}
