namespace DoorMonitorSystem.Models.system
{
    public class NtpConfig
    {
        /// <summary>
        /// 是否启用客户端同步本机
        /// </summary>
        public bool IsNtpClientEnabled { get; set; } = false;

        /// <summary>
        /// 上级服务器地址，支持分号分隔 (e.g. "192.168.1.10;pool.ntp.org")
        /// </summary>
        public string NtpServerUrl { get; set; } = "pool.ntp.org";

        /// <summary>
        /// 同步间隔 (分钟)
        /// </summary>
        public int NtpSyncIntervalMinutes { get; set; } = 60;

        /// <summary>
        /// 是否启用服务端对外授时
        /// </summary>
        public bool IsNtpServerEnabled { get; set; } = false;
    }
}
