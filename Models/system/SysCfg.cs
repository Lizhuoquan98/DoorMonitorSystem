  
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
    }
}
