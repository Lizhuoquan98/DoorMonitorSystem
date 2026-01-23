using System.Diagnostics;

namespace DoorMonitorSystem.Assets.Helper
{
    /// <summary>
    /// 自定义 TraceListener，将系统调试输出重定向到文件日志
    /// </summary>
    public class LogTraceListener : TraceListener
    {
        public override void Write(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                // Trace.Write 不换行，但我们的 Logger 是按行记录的，暂时当作一行处理
                LogHelper.WriteLog(message.TrimEnd(), "DEBUG");
            }
        }

        public override void WriteLine(string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                LogHelper.WriteLog(message, "DEBUG");
            }
        }
    }
}
