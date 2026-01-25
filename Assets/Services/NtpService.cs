using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.system;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 自主实现的 NTP 服务 (SNTP 协议)
    /// 功能：
    /// 1. 客户端：从上级服务器同步本机时间 (需要管理员权限)
    /// 2. 服务端：本机作为 NTP 服务器对外授时 (需占用 UDP 123)
    /// </summary>
    public class NtpService : IDisposable
    {
        private static NtpService? _instance;
        public static NtpService Instance => _instance ??= new NtpService();

        private Timer? _syncTimer;
        private UdpClient? _serverSocket;
        private bool _isRunning = false;
        private CancellationTokenSource? _cts;

        // NTP Packet Offset Constants
        private const byte NTP_MODE_CLIENT = 3;
        private const byte NTP_MODE_SERVER = 4;
        private const int NTP_PORT = 123;
        private const long WIN_TICKS_PER_SEC = 10000000;
        private const long WIN_TICKS_1900_TO_1970 = 621355968000000000; // Ticks delta between 1900 and 1970
        
        #region Win32 API (For Setting Time)

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetLocalTime(ref SYSTEMTIME lpSystemTime);

        #endregion

        /// <summary>
        /// 启动 NTP 服务 (根据 GlobalData.NtpConfig)
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            var config = GlobalData.NtpConfig;
            if (config == null) 
            {
                LogHelper.Info("[NtpService] 未配置 NTP，跳过启动");
                return;
            }

            if (!config.IsNtpClientEnabled && !config.IsNtpServerEnabled)
            {
                LogHelper.Info("[NtpService] 客户端和服务端均未启用");
                return;
            }

            _isRunning = true;
            _cts = new CancellationTokenSource();

            // 1. 如果启用了服务端，必须先解决端口冲突 (停止 w32time)
            if (config.IsNtpServerEnabled)
            {
                await StopWindowsTimeServiceAsync();
                StartNtpServer();
            }

            // 2. 如果启用了客户端，启动定时同步任务
            if (config.IsNtpClientEnabled)
            {
                int intervalMs = Math.Max(1, config.NtpSyncIntervalMinutes) * 60 * 1000;
                
                // 立即执行一次同步 (异步，不阻塞启动流程)
                _ = Task.Run(async () => await SyncTimeAsync());

                // 启动定时器后续同步
                _syncTimer = new Timer(async _ => await SyncTimeAsync(), null, intervalMs, intervalMs); 
                LogHelper.Info($"[NtpService] 客户端同步任务已启动，间隔: {config.NtpSyncIntervalMinutes} 分钟 (正在执行首次同步...)");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _syncTimer?.Dispose();
            _cts?.Cancel();
            _serverSocket?.Close();
            _serverSocket = null;
            LogHelper.Info("[NtpService] 服务已停止");
        }

        public void Dispose()
        {
            Stop();
        }

        #region NTP Client Logic

        /// <summary>
        /// 执行一次时间同步
        /// </summary>
        public async Task SyncTimeAsync()
        {
            try
            {
                var config = GlobalData.NtpConfig;
                if (config == null || string.IsNullOrWhiteSpace(config.NtpServerUrl)) return;

                string[] servers = config.NtpServerUrl.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
                
                foreach (var serverHost in servers)
                {
                    try
                    {
                        var targetTime = await GetNetworkTimeAsync(serverHost.Trim());
                        if (targetTime != DateTime.MinValue)
                        {
                            if (ApplySystemTime(targetTime))
                            {
                                LogHelper.Info($"[NtpService] 成功从 {serverHost} 同步时间: {targetTime}");
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Warn($"[NtpService] {serverHost} 同步失败: {ex.Message}");
                    }
                }
                LogHelper.Error("[NtpService] 所有 NTP 服务器均无法连接或校时失败");
            }
            catch (Exception ex)
            {
                LogHelper.Error("[NtpService] SyncTimeAsync 全局异常", ex);
            }
        }

        private async Task<DateTime> GetNetworkTimeAsync(string ntpServer)
        {
            // RFC 2030 SNTP Packet
            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            using (var client = new UdpClient())
            {
                // 注意: ReceiveAsync 不受 Socket.ReceiveTimeout 影响，需手动处理超时
                
                // 1. 发送请求
                await client.SendAsync(ntpData, ntpData.Length, ntpServer, NTP_PORT);
                
                // 2. 接收响应 (带超时)
                var receiveTask = client.ReceiveAsync();
                var timeoutTask = Task.Delay(3000); // 3秒超时

                var completedTask = await Task.WhenAny(receiveTask, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    // 超时
                    throw new System.TimeoutException("NTP Response Timeout");
                }

                var result = await receiveTask;
                var respBytes = result.Buffer;

                if (respBytes.Length < 48) return DateTime.MinValue;

                // Offset 40: Transmit Timestamp (64-bit)
                // Integer part (seconds since 1900-01-01)
                ulong intPart = (ulong)respBytes[40] << 24 | (ulong)respBytes[41] << 16 | (ulong)respBytes[42] << 8 | (ulong)respBytes[43];
                // Fraction part
                ulong fractPart = (ulong)respBytes[44] << 24 | (ulong)respBytes[45] << 16 | (ulong)respBytes[46] << 8 | (ulong)respBytes[47];

                var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                
                // UTC Time
                var networkDateTime = (new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc)).AddMilliseconds((long)milliseconds);
                
                return networkDateTime.ToLocalTime();
            }
        }

        private bool ApplySystemTime(DateTime dt)
        {
            // 尝试启用权限
            EnableProcessPrivilege("SeSystemtimePrivilege");

            SYSTEMTIME st = new SYSTEMTIME
            {
                wYear = (ushort)dt.Year,
                wMonth = (ushort)dt.Month,
                wDay = (ushort)dt.Day,
                wHour = (ushort)dt.Hour,
                wMinute = (ushort)dt.Minute,
                wSecond = (ushort)dt.Second,
                wMilliseconds = (ushort)dt.Millisecond
            };

            if (!SetLocalTime(ref st))
            {
                int err = Marshal.GetLastWin32Error();
                // 1314 = ERROR_PRIVILEGE_NOT_HELD
                LogHelper.Error($"[NtpService] 修改本机时间失败 (Error {err})。请确保软件以【管理员身份】运行！");
                return false;
            }
            return true;
        }

        private void EnableProcessPrivilege(string privilegeName)
        {
            try
            {
                IntPtr hToken;
                if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken)) return;

                try
                {
                    LUID luid;
                    if (!LookupPrivilegeValue(null, privilegeName, out luid)) return;

                    TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                    tp.PrivilegeCount = 1;
                    tp.Privileges = new LUID_AND_ATTRIBUTES[1];
                    tp.Privileges[0].Luid = luid;
                    tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

                    AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    CloseHandle(hToken);
                }
            }
            catch { }
        }

        #region Win32 API Definitions

        private const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const int TOKEN_QUERY = 0x0008;
        private const int SE_PRIVILEGE_ENABLED = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        #endregion

        #endregion

        #region NTP Server Logic

        private void StartNtpServer()
        {
            try
            {
                // 绑定所有网卡的 UDP 123 端口
                _serverSocket = new UdpClient(NTP_PORT);
                LogHelper.Info("[NtpService] NTP 服务端已启动，监听 UDP 123");

                // 开始异步接收循环
                Task.Run(ServerLoopAsync);
            }
            catch (SocketException sex)
            {
                if (sex.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    LogHelper.Error("[NtpService] 端口 123 被占用。请检查 Windows Time 服务是否已停止。");
                }
                else
                {
                    LogHelper.Error("[NtpService] 启动服务端失败", sex);
                }
            }
        }

        private async Task ServerLoopAsync()
        {
            if (_serverSocket == null) return;
            while (_isRunning && _serverSocket != null)
            {
                try
                {
                    var result = await _serverSocket.ReceiveAsync();
                    byte[] request = result.Buffer;
                    
                    if (request.Length < 48) continue; // Ignore malformed packets

                    // 构造响应
                    byte[] response = new byte[48];
                    
                    // LI=0, VN=3, Mode=4 (Server) -> 0x1C
                    response[0] = 0x1C; 
                    response[1] = 1; // Stratum 1 (Primary Reference)
                    response[2] = 6; // Poll interval
                    response[3] = 0xEC; // Precision

                    // Root Delay & Dispersion (Mock values)
                    // ... Skipped for simplicity ...

                    // Copy "Transmit Timestamp" from Request to "Origin Timestamp" in Response (Offset 24)
                    Array.Copy(request, 40, response, 24, 8);

                    // Receive Timestamp (now) -> Offset 32
                    long nowTicks = DateTime.UtcNow.Ticks;
                    WriteTimestamp(response, 32, nowTicks);

                    // Transmit Timestamp (now) -> Offset 40
                    WriteTimestamp(response, 40, nowTicks);

                    await _serverSocket.SendAsync(response, response.Length, result.RemoteEndPoint);
                    // 仅在调试时打开，避免日志爆炸
                    // LogHelper.Debug($"[NtpService] 回复客户端 {result.RemoteEndPoint}");
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    LogHelper.Error($"[NtpService] 服务端异常: {ex.Message}");
                    await Task.Delay(1000); 
                }
            }
        }

        private void WriteTimestamp(byte[] buffer, int offset, long ticks)
        {
            TimeSpan span = new TimeSpan(ticks - new DateTime(1900, 1, 1).Ticks);
            double seconds = span.TotalSeconds;

            ulong intPart = (ulong)seconds;
            ulong fractPart = (ulong)((seconds - intPart) * 0x100000000L);

            // Big Endian
            buffer[offset] = (byte)(intPart >> 24);
            buffer[offset + 1] = (byte)(intPart >> 16);
            buffer[offset + 2] = (byte)(intPart >> 8);
            buffer[offset + 3] = (byte)(intPart);

            buffer[offset + 4] = (byte)(fractPart >> 24);
            buffer[offset + 5] = (byte)(fractPart >> 16);
            buffer[offset + 6] = (byte)(fractPart >> 8);
            buffer[offset + 7] = (byte)(fractPart);
        }

        #endregion

        #region Windows Service Helper

        private async Task StopWindowsTimeServiceAsync()
        {
            try
            {
                ServiceController sc = new ServiceController("w32time");
                if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                {
                    LogHelper.Info("[NtpService] 正在停止 Windows Time 服务以释放端口...");
                    sc.Stop();
                    // 等待停止
                    var timeout = TimeSpan.FromSeconds(5);
                    var start = DateTime.Now;
                    while (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        sc.Refresh();
                        if (DateTime.Now - start > timeout) break;
                        await Task.Delay(500);
                    }
                    LogHelper.Info("[NtpService] Windows Time 服务已停止");
                }
            }
            catch (Exception ex)
            {
                // 如果没有权限或服务不存在
                LogHelper.Warn($"[NtpService] 停止 w32time 失败 (可能需要管理员权限): {ex.Message}");
            }
        }

        #endregion
    }
}
