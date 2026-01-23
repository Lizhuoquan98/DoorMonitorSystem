using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DoorMonitorSystem
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 初始化日志系统
            DoorMonitorSystem.Assets.Helper.LogHelper.CleanupOldLogs();

            // 2. 注册 Trace监听器，捕获所有 Debug.WriteLine 输出
            System.Diagnostics.Trace.Listeners.Add(new DoorMonitorSystem.Assets.Helper.LogTraceListener());
            
            DoorMonitorSystem.Assets.Helper.LogHelper.Info("=== Application Started ===");

            // 3. 全局异常捕获
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            DoorMonitorSystem.Assets.Helper.LogHelper.Error("UI Unhandled Exception", e.Exception);
            // e.Handled = true; // 可选：是否阻止崩溃
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                DoorMonitorSystem.Assets.Helper.LogHelper.Error("Domain Unhandled Exception", ex);
            }
        }
    }
}
