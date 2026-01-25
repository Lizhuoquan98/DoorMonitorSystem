
namespace DoorMonitorSystem.Models.system
{
    public class AppDebugConfig
    {
        // --- 调试日志开关 (Debug Trace Switches) ---
        /// <summary>
        /// 调试说明: 设置为 true 可在日志中输出详细的点位读写与解析信息，用于排查问题。
        /// S7_Detail: S7原始字节解析; Modbus_Detail: Modbus高低字解析; Forward_Detail: 转发写入详情
        /// </summary>
        public string _Debug_Help { get; set; } = "Set Trace_* to true to enable detailed logs for troubleshooting.";
        
        public bool Trace_S7_Detail { get; set; } = false;
        public bool Trace_Modbus_Detail { get; set; } = false;
        public bool Trace_Forwarding_Detail { get; set; } = false;
        public bool Trace_Communication_Raw { get; set; } = false;

        /// <summary>
        /// 日志保留月数 (默认12个月)
        /// </summary>
        public int LogRetentionMonths { get; set; } = 12;

        /// <summary>
        /// 每日自动清理时间 (默认凌晨 01:00:00)
        /// </summary>
        public string LogCleanupTime { get; set; } = "01:00:00";
    }
}
