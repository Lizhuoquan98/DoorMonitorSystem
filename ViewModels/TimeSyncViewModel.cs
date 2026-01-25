using Base;
using Communicationlib.config;
using DoorMonitorSystem.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 校时配置视图模型
    /// 负责 TimeSyncWindow 的业务逻辑，包括配置的绑定、验证以及互斥检查。
    /// </summary>
    public class TimeSyncViewModel : NotifyPropertyChanged
    {
        /// <summary>
        /// 核心配置实体
        /// </summary>
        public TimeSyncConfig Config { get; private set; }
        
        /// <summary>
        /// 窗口关闭回调
        /// </summary>
        public Action? CloseAction { get; set; }
        
        /// <summary>
        /// 验证回调：检查当前是否允许设置为 "受时" (PLC -> PC)。
        /// 用于确保全局只有一个设备作为受时源。
        /// </summary>
        public Func<bool>? ValidateCanSetAsUpstream { get; set; }
        
        /// <summary>
        /// 协议类型标记 (是否为客户端 ProtocolClient)
        /// True: Client模式 (Active)
        /// False: Server模式 (Passive)
        /// </summary>
        public bool IsProtocolClient { get; set; } = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">时间同步配置对象</param>
        /// <param name="isClient">是否为客户端设备</param>
        /// <param name="validateUpstream">受时互斥验证委托</param>
        public TimeSyncViewModel(TimeSyncConfig config, bool isClient, Func<bool>? validateUpstream = null)
        {
            // 直接引用配置对象
            Config = config ?? new TimeSyncConfig();
            IsProtocolClient = isClient;
            ValidateCanSetAsUpstream = validateUpstream;
        }

        /// <summary>
        /// 同步方向索引 (用于绑定界面 ComboBox)
        /// 0: 下发 (PC -> PLC) - 默认
        /// 1: 受时 (PLC -> PC)
        /// </summary>
        public int Direction
        {
            get => Config.Direction;
            set
            {
                if (Config.Direction != value)
                {
                    // 检查互斥：如果试图设置为 "受时" (1)，且当前不是(1)
                    if (value == 1)
                    {
                        if (ValidateCanSetAsUpstream != null && !ValidateCanSetAsUpstream())
                        {
                            MessageBox.Show("错误：全局只能配置一个设备作为【本机受时源】！\n请先取消其他设备的受时配置。", "互斥保护", MessageBoxButton.OK, MessageBoxImage.Warning);
                            // 保持原值 (Force UI update)
                            OnPropertyChanged(); 
                            return;
                        }
                    }
                    
                    Config.Direction = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDownstream));
                }
            }
        }
        
        /// <summary>
        /// 是否为下发模式 (0 = PC -> PLC)
        /// 用于控制界面 Grid/StackPanel 的 Visibility (上行受时模式不需要配置复杂的下发策略)
        /// </summary>
        public bool IsDownstream => Config.Direction == 0;

        /// <summary>
        /// 保存命令
        /// 关闭窗口 (数据直接绑定在 Config 对象上，无需额外 Save 操作，但预留扩展)
        /// </summary>
        public ICommand SaveCommand => new RelayCommand(_ =>
        {
            CloseAction?.Invoke();
        });

        /// <summary>
        /// 取消/关闭命令
        /// </summary>
        public ICommand CloseCommand => new RelayCommand(_ =>
        {
            CloseAction?.Invoke();
        });
        
        // --- 辅助属性 (简化 XAML RadioButton 绑定) ---
        
        /// <summary>
        /// 调度模式：周期执行
        /// </summary>
        public bool IsIntervalMode 
        { 
            get => Config.ScheduleMode == 0; 
            set { if(value) Config.ScheduleMode = 0; OnPropertyChanged(); OnPropertyChanged(nameof(IsFixedTimeMode)); } 
        }
        
        /// <summary>
        /// 调度模式：定点执行
        /// </summary>
         public bool IsFixedTimeMode 
        { 
            get => Config.ScheduleMode == 1; 
            set { if(value) Config.ScheduleMode = 1; OnPropertyChanged(); OnPropertyChanged(nameof(IsIntervalMode)); } 
        }

        /// <summary>
        /// 策略：单机 (当前设备)
        /// </summary>
        public bool IsStrategySelf 
        { 
            get => Config.Strategy == 0; 
            set { if(value) Config.Strategy = 0; OnPropertyChanged(); UpdateStrategy(); } 
        }
        
        /// <summary>
        /// 策略：广播 (255)
        /// </summary>
        public bool IsStrategyBroadcast 
        { 
            get => Config.Strategy == 1; 
            set { if(value) Config.Strategy = 1; OnPropertyChanged(); UpdateStrategy(); } 
        }
        
        /// <summary>
        /// 策略：范围轮询 (自定义 ID 列表)
        /// </summary>
        public bool IsStrategyRange 
        { 
            get => Config.Strategy == 2; 
            set { if(value) Config.Strategy = 2; OnPropertyChanged(); UpdateStrategy(); } 
        }

        private void UpdateStrategy()
        {
            OnPropertyChanged(nameof(IsStrategySelf));
            OnPropertyChanged(nameof(IsStrategyBroadcast));
            OnPropertyChanged(nameof(IsStrategyRange));
        }
    }
}
