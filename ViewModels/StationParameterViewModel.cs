using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using Base;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Assets.Services;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.ConfigEntity;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 单个站台侧的参数配置视图模型
    /// 封装了该侧站台的所有业务逻辑：型号选择、门号联动、权限校验、读写操作及日志记录。
    /// 数据驱动：参数定义与型号映射均动态从数据库持久化层加载。
    /// </summary>
    public class StationParameterViewModel : NotifyPropertyChanged
    {
        #region 字段与属性

        private string _stationName;
        private ObservableCollection<ParameterItem> _parameterList;
        private ObservableCollection<AsdModelMapping> _asdModels;
        private AsdModelMapping _selectedAsdModel;
        private int _doorId = 1;
        private int? _targetDeviceId;
        private int? _stationId;

        /// <summary>站台显示名称 (如: 上行站台)</summary>
        public string StationName
        {
            get => _stationName;
            set { _stationName = value; OnPropertyChanged(); }
        }

        /// <summary>当前站台侧呈现的 ASD 参数集合</summary>
        public ObservableCollection<ParameterItem> ParameterList
        {
            get => _parameterList;
            set { _parameterList = value; OnPropertyChanged(); }
        }

        /// <summary>可选的 ASD 型号及其默认物理 ID 映射表</summary>
        public ObservableCollection<AsdModelMapping> AsdModels
        {
            get => _asdModels;
            set { _asdModels = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 当前选中的 ASD 型号
        /// 变更时会触发【正向联动】：自动将物理门号同步为该型号的默认物理 ID，并触发点位重新绑定。
        /// </summary>
        public AsdModelMapping SelectedAsdModel
        {
            get => _selectedAsdModel;
            set 
            { 
                if (_selectedAsdModel == value) return;
                _selectedAsdModel = value; 
                OnPropertyChanged(); 
                
                // 正向联动：选择型号后，自动同步门号 (保持默认推荐值)
                if (value != null)
                {
                    _doorId = value.PlcId;
                    OnPropertyChanged(nameof(DoorId));
                    // 只有在切换型号（即切换物理门组）时，才需要重新查找点位绑定
                    BindConfigToParameters();
                }
            }
        }

        /// <summary>
        /// 当前通讯对应的物理门号 (即物理寻址 ID)
        /// 变更时会尝试反向联动更新型号选择，但不直接触发耗时的重绑定逻辑（除非型号发生变化）。
        /// </summary>
        public int DoorId
        {
            get => _doorId;
            set 
            { 
                if (_doorId == value) return;
                _doorId = value; 
                OnPropertyChanged(); 

                // 反向联动：手动输入 ID 时，尝试自动匹配并选中对应的型号
                var match = AsdModels?.FirstOrDefault(m => m.PlcId == value);
                if (match != null && match != _selectedAsdModel)
                {
                    SelectedAsdModel = match; // 这会进而触发 SelectedAsdModel 的逻辑（包括重绑定）
                }
            }
        }

        /// <summary>绑定的目标设备ID (用于过滤不同 PLC 下的同名键名，防止数据串位)</summary>
        public int? TargetDeviceId 
        { 
            get => _targetDeviceId; 
            set { _targetDeviceId = value; OnPropertyChanged(); BindConfigToParameters(); } 
        }

        /// <summary>站台关联的数据库ID (用于精准匹配 TargetType=3 & TargetObjId=StationId 的点位)</summary>
        public int? StationId 
        { 
            get => _stationId; 
            set { _stationId = value; OnPropertyChanged(); BindConfigToParameters(); } 
        }

        #endregion

        #region 命令定义

        /// <summary>写入参数指令</summary>
        public ICommand SaveCommand { get; }
        /// <summary>同步/查询参数指令</summary>
        public ICommand ReadCommand { get; }
        /// <summary>重置/清空本地输入指令</summary>
        public ICommand ResetCommand { get; }

        #endregion

        /// <summary>
        /// 自动刷新定时器
        /// </summary>
        private System.Windows.Threading.DispatcherTimer _refreshTimer;

        /// <param name="stationName">站台侧名称</param>
        /// <param name="models">全局预设的型号映射表 (此时已从数据库加载)</param>
        /// <param name="targetDeviceId">目标设备ID (可选，用于精确区分多站台)</param>
        /// <param name="stationId">站台数据库ID (可选，用于精确关联参数点位)</param>
        public StationParameterViewModel(string stationName, ObservableCollection<AsdModelMapping> models, int? targetDeviceId = null, int? stationId = null)
        {
            StationName = stationName;
            TargetDeviceId = targetDeviceId;
            StationId = stationId;

            AsdModels = models;
            _selectedAsdModel = AsdModels.FirstOrDefault();

            // 订阅通信服务加载完成事件 (解决启动时数据库读取的时序问题)
            if (DeviceCommunicationService.Instance != null)
            {
                DeviceCommunicationService.Instance.ConfigLoaded += () => 
                {
                    // 在 UI 线程执行重新绑定
                    Application.Current.Dispatcher.Invoke(BindConfigToParameters);
                };
            }
            
            // 1. 从数据库动态加载参数定义，而非硬编码
            LoadParameterListFromDb();

            // 2. 权限初始化：根据当前登录用户角色校验编辑权限
            UpdatePermissions();

            // 3. 绑定业务逻辑命令
            SaveCommand = new RelayCommand(ExecuteSave, CanExecuteSave);
            ReadCommand = new RelayCommand(ExecuteRead);
            ResetCommand = new RelayCommand(obj => { 
                foreach(var item in ParameterList) item.Value = string.Empty;
                MessageBox.Show($"{StationName}: 填写数据已清空。", "系统提示"); 
            });

            // 4. 启动自动刷新定时器 (提升至 200ms 一次，确保 1Hz 变化的源数据不跳帧)
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(200);
            _refreshTimer.Tick += (s, e) => RefreshRealtimeData();
            _refreshTimer.Start();

            // 立即执行一次初始刷新
            BindConfigToParameters();
        }

        public void BindConfigToParameters()
        {
            // 如果通信服务还没从数据库加载完配置，则先跳过绑定（等待 ConfigLoaded 事件通知）
            if (DeviceCommunicationService.Instance == null || !DeviceCommunicationService.Instance.IsInitialized)
            {
                LogHelper.Debug($"[ParamBind] {StationName} 暂时跳过绑定: 通讯服务加载中...");
                return;
            }

            if (ParameterList == null) return;
            // 简单防抖
            if (DoorId <= 0) return;

            // 获取该门号下所有的点位配置
            // 注意：这里假设 DeviceCommunicationService 已经加载了所有点位
            // 我们不依赖 GetPointConfigForDoorRelaxed 的单次查找，而是获取列表进行内存匹配
            // 这里我们需要一个新的帮助方法 GetPointsForDoor 在 Service 中，或者我们在此处循环查找
            // 为了不修改 Service 接口，我们仍然使用 GetPointConfigForDoorRelaxed，但只做一次

            LogHelper.Info($"[ParamBind] 开始站台参数绑定: StationName={StationName}, StationId={StationId}, TargetDeviceId={TargetDeviceId}");

            int foundCount = 0;
            foreach (var item in ParameterList)
            {
                // 核心逻辑：UI参数项与点位配置的“物理-逻辑”关联。
                
                DevicePointConfigEntity cfg = null;
                if (StationId.HasValue)
                {
                    // 1. 站台级精准寻址：查找明确绑定到此站台 (TargetType=Station) 的点位。
                    // 优先匹配 BindingRole 为 "Read" 的点位（最符合参数实时显示的语义）。
                    cfg = DeviceCommunicationService.Instance?.GetPointConfigForStation(StationId.Value, item.BindingKey, "Read", TargetDeviceId);
                    
                    // 2. 备选逻辑：如果没找到 Read 角色，尝试该站台下同键名但非 Write 角色的点位。
                    // 目的是兼容那些没设角色或者角色设为 Parameter 的点位，但严禁将“写入点位”当做“读取来源”。
                    if (cfg == null)
                    {
                        var allConfigs = DeviceCommunicationService.Instance?.GetPointConfigsForStation(StationId.Value);
                        cfg = allConfigs?.FirstOrDefault(p => string.Equals(p.UiBinding, item.BindingKey, StringComparison.OrdinalIgnoreCase) 
                                                           && p.BindingRole != "Write"); 
                    }
                }

                // 3. 全局降级匹配：如果上述精准匹配失败（点表中可能没填所属对象 ID）。
                // 则回退到通过 BindingKey 在全局点位 (TargetType=None) 中查找。
                if (cfg == null)
                {
                    cfg = DeviceCommunicationService.Instance?.GetPointConfigByKey(item.BindingKey, "Read", TargetDeviceId);
                }
                
                // 4. 最终尝试：不限角色的全局匹配。
                if (cfg == null)
                {
                    cfg = DeviceCommunicationService.Instance?.GetPointConfigByKey(item.BindingKey, null, TargetDeviceId);
                }

                item.Config = cfg;

                if (cfg != null)
                {
                    item.DebugInfo = $"已绑定: Addr={cfg.Address}, Role={cfg.BindingRole}, ObjId={cfg.TargetObjId}";
                    foundCount++;
                    
                    if (cfg.LastValue != null)
                    {
                        string val = cfg.LastValue.ToString();
                        item.ReadbackValue = val;
                        if (string.IsNullOrEmpty(item.Value)) item.Value = val;
                    }
                }
                else
                {
                    item.DebugInfo = $"未绑定: {item.BindingKey}";
                    // 只有在 ID 解析出来后才输出警告日志，避免启动瞬间产生的无效警告
                    if (TargetDeviceId.HasValue)
                    {
                        LogHelper.Warn($"[ParamBind] {StationName} 匹配失败: Key='{item.BindingKey}'。请检查点表是否有 TargetType=3 & TargetObjId={StationId} & Role=Read 的配置。");
                    }
                }
            }
            LogHelper.Info($"[ParamBind] {StationName} 绑定检查完成。成功: {foundCount}/{ParameterList.Count} (TargetDev={TargetDeviceId})");
        }

        private void RefreshRealtimeData()
        {
            if (ParameterList == null) return;

            foreach (var item in ParameterList)
            {
                // 直接使用缓存的 Config 引用，无需再次查找
                if (item.Config != null && item.Config.LastValue != null)
                {
                    string newVal = item.Config.LastValue.ToString();
                    
                    if (item.ReadbackValue != newVal)
                    {
                        item.ReadbackValue = newVal;
                        
                        // 移除自动填充逻辑，避免用户清空输入框时被反向覆盖
                        // if (string.IsNullOrEmpty(item.Value) && !string.IsNullOrEmpty(newVal))
                        // {
                        //     item.Value = newVal;
                        // }
                    }
                }
            }
        }

        /// <summary>
        /// 从数据库加载参数定义模板
        /// </summary>
        private void LoadParameterListFromDb()
        {
            var defines = DataManager.Instance.LoadParameterDefinesFromDb();
            var items = defines.Select(d => new ParameterItem
            {
                Label = d.Label,
                Unit = d.Unit,
                Hint = d.Hint,
                BindingKey = d.BindingKey,
                PlcPermissionValue = d.PlcPermissionValue, // 加载设备下发鉴权值
                DataType = d.DataType // 加载参数数据类型
            }).ToList();

            ParameterList = new ObservableCollection<ParameterItem>(items);
        }

        /// <summary>
        /// 执行权限校验逻辑 (全局 UI 锁定)
        /// 如果是 Operator，则全部置灰；如果是 Admin/Engineer，则全部可编辑。
        /// </summary>
        public void UpdatePermissions()
        {
            // 获取当前登录用户的角色字符串，默认为 Operator
            string userRole = GlobalData.CurrentUser?.Role ?? "Operator";
            bool isOperator = userRole.Equals("Operator", StringComparison.OrdinalIgnoreCase);

            // 如果是操作员，只读；非操作员，可写
            bool canEdit = !isOperator;

            foreach (var item in ParameterList)
            {
                item.IsEditable = canEdit;
            }
        }

        /// <summary>
        /// 判断是否允许执行保存 (针对按钮的命令状态)
        /// </summary>
        private bool CanExecuteSave(object obj)
        {
             // 总是允许点击，为了在点击时弹出权限提示
             return true;
        }

        #region 核心业务逻辑实现

        /// <summary>
        /// 执行参数写入过程
        /// </summary>
        private async void ExecuteSave(object obj)
        {
             // 权限拦截：如果是 Operator，则提示禁止写入
             string userRole = GlobalData.CurrentUser?.Role ?? "Operator";
             if (userRole.Equals("Operator", StringComparison.OrdinalIgnoreCase))
             {
                 MessageBox.Show("当前用户权限（Operator）仅允许读取，禁止写入参数！\n请联系管理员获取权限。", "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                 return;
             }

            if (SelectedAsdModel == null) return;

            // 强制一致性：如果门号与物理地址不一致，则覆盖门号
            if (DoorId != SelectedAsdModel.PlcId)
            {
                DoorId = SelectedAsdModel.PlcId;
            }

            // 智能过滤：仅下发用户输入的内容
            var validItems = ParameterList.Where(p => !string.IsNullOrWhiteSpace(p.Value)).ToList();
            if (validItems.Count == 0)
            {
                MessageBox.Show("请至少输入一个需要修改的参数值（留空项将跳过修改）。", "操作提示");
                return;
            }

            // 构建交互确认信息
            string msg = $"[{StationName}] 正在准备物理寻址下发：\n" +
                         $"配置项目: {SelectedAsdModel.DisplayName} (物理ID: {DoorId})\n\n" +
                         $"即将修改以下参数：\n";

            // 预查找写入配置
            var writeConfigs = new Dictionary<ParameterItem, DevicePointConfigEntity>();

            foreach (var item in validItems)
            {
                // 查找写入配置 (优先级：Station-Write > Station-Parameter > Key-Write > Fallback)
                DevicePointConfigEntity writeCfg = null;

                // 1. 优先：站台级精准寻址
                if (StationId.HasValue)
                {
                    writeCfg = DeviceCommunicationService.Instance?.GetPointConfigForStation(StationId.Value, item.BindingKey, "Write", TargetDeviceId);
                    if (writeCfg == null)
                    {
                        writeCfg = DeviceCommunicationService.Instance?.GetPointConfigForStation(StationId.Value, item.BindingKey, "Parameter", TargetDeviceId);
                    }
                }

                // 2. 次选：全局 Key 匹配 (针对未分配 TargetObjId 的点位)
                if (writeCfg == null)
                {
                    writeCfg = DeviceCommunicationService.Instance?.GetPointConfigByKey(item.BindingKey, "Write", TargetDeviceId);
                }
                if (writeCfg == null) {
                    writeCfg = DeviceCommunicationService.Instance?.GetPointConfigByKey(item.BindingKey, "Parameter", TargetDeviceId);
                }

                // 3. 最后保底
                if (writeCfg == null)
                {
                    writeCfg = item.Config; // 使用加载时找到的配置
                    
                    if (writeCfg == null)
                    {
                         writeCfg = DeviceCommunicationService.Instance?.GetPointConfigByKey(item.BindingKey, null, TargetDeviceId);
                    }
                }

                if (writeCfg != null) writeConfigs[item] = writeCfg;

                string addrInfo = writeCfg != null ? $" -> Addr:{writeCfg.Address} (Role:{writeCfg.BindingRole})" : " [Unmapped]";
                msg += $"- {item.Label}: {item.Value} {item.Unit} {addrInfo}\n";
            }
            
            var result = MessageBox.Show(msg + "\n请确认无误后点击确定完成写入操作。", "物理 ID 寻址确认", MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK) return;

            int successCount = 0;
            string failLog = "";

            // 执行参数值写入
            foreach (var item in validItems)
            {
                 if (writeConfigs.TryGetValue(item, out var pointConfig))
                 {
                     bool success = await DeviceCommunicationService.Instance.WritePointValueAsync(pointConfig, item.Value);
                     if (success) successCount++;
                     else failLog += $"{item.Label} 写入失败\n";
                 }
                 else
                 {
                     failLog += $"{item.Label} 未找到点位配置\n";
                 }
            }

            // --- 核心控制逻辑：下发门号 -> 延时 -> 触发写入指令 ---
            try 
            {
                if (StationId.HasValue)
                {
                    // 1. 下发门号 (Sys_DoorId)
                    var doorIdPoint = DeviceCommunicationService.Instance?.GetPointConfigForStation(StationId.Value, "Sys_DoorId", "Write", TargetDeviceId);
                    if (doorIdPoint != null)
                    {
                        await DeviceCommunicationService.Instance.WritePointValueAsync(doorIdPoint, DoorId.ToString());
                        LogHelper.Info($"[ParamSet] 已下发目标门号: {DoorId}");
                    }

                    // 2. 延时 100 毫秒 (硬件时序要求)
                    await Task.Delay(100);

                    // 3. 下发写入触发状态 (Sys_WriteTrigger)
                    var writeTriggerPoint = DeviceCommunicationService.Instance?.GetPointConfigForStation(StationId.Value, "Sys_WriteTrigger", "Write", TargetDeviceId);
                    if (writeTriggerPoint != null)
                    {
                        await DeviceCommunicationService.Instance.WritePointValueAsync(writeTriggerPoint, "1");
                        LogHelper.Info($"[ParamSet] 已下发写入触发指令 (Sys_WriteTrigger)");
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[ParamSet] 控制指令下发异常", ex);
                failLog += "控制指令发送异常\n";
            }

            // 结果反馈
            string logSummary = $"[{StationName} - {SelectedAsdModel.DisplayName} 门号:{DoorId}] 写入参数: " +
                                string.Join(", ", validItems.Select(x => $"{x.Label}={x.Value}"));
            
            if (string.IsNullOrEmpty(failLog))
            {
                LogService.Instance.AddCustomLog(logSummary + " [成功]", "参数设置");
                MessageBox.Show($"写入成功！共下发 {successCount} 个参数，并触发了 PLC 写入逻辑。", "操作成功");
            }
            else
            {
                LogService.Instance.AddCustomLog(logSummary + " [部分失败]", "参数设置");
                MessageBox.Show($"写入完成，但有部分错误：\n{failLog}", "操作警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        /// <summary>
        /// 执行参数读取刷新过程
        /// </summary>
        private async void ExecuteRead(object obj)
        {
            if (SelectedAsdModel == null || !StationId.HasValue) return;

            // 1. 强制寻址门号一致性
            if (DoorId != SelectedAsdModel.PlcId)
            {
                DoorId = SelectedAsdModel.PlcId;
            }

            try 
            {
                // A. 下发目标门号 (Sys_DoorId)
                var doorIdPoint = DeviceCommunicationService.Instance?.GetPointConfigForStation(StationId.Value, "Sys_DoorId", "Write", TargetDeviceId);
                if (doorIdPoint != null)
                {
                    await DeviceCommunicationService.Instance.WritePointValueAsync(doorIdPoint, DoorId.ToString());
                }

                // B. 等待 100ms 硬件处理时延
                await Task.Delay(100);

                // C. 下发读取触发指令 (Sys_ReadTrigger)
                var readTriggerPoint = DeviceCommunicationService.Instance?.GetPointConfigForStation(StationId.Value, "Sys_ReadTrigger", "Write", TargetDeviceId);
                if (readTriggerPoint != null)
                {
                    await DeviceCommunicationService.Instance.WritePointValueAsync(readTriggerPoint, "1");
                }
                
                LogHelper.Info($"[ParamRead] 已下发读取触发序列：DoorId={DoorId}");
            }
            catch (Exception ex)
            {
                LogHelper.Error("[ParamRead] 读取触发指令下发异常", ex);
            }

            string logMsg = $"[{StationName} - {SelectedAsdModel.DisplayName} 门号:{DoorId}] 正在从物理地址读取最新参数...";
            MessageBox.Show($"[{StationName}] 已向上位机发送读取刷新请求，PLC 参数即将同步。", "刷新请求已发送");
            
            LogService.Instance.AddCustomLog(logMsg, "参数设置");
            
            // 延时一下等 PLC 响应后再刷新 UI 表现（可选）
            await Task.Delay(200);
            RefreshRealtimeData();
        }

        #endregion
    }
}
