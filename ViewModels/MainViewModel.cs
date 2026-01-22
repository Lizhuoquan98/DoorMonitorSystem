using Base;
using DoorMonitorSystem.Assets.Services;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.RunModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 主界面视图模型（完全数据驱动）
    /// 通过 StationViewModel 集合管理所有站台
    /// </summary>
    public class MainViewModel : NotifyPropertyChanged
    {
        #region 字段

        private CancellationTokenSource _updateLoopTokenSource = new();
        private bool _isPopupOpen;
        private string _popupTitle = "";
        private DoorModel? _selectedDoor;

        #endregion

        #region 属性

        /// <summary>
        /// 站台视图模型集合（数据驱动核心）
        /// UI直接绑定此集合，自动渲染所有站台
        /// </summary>
        public ObservableCollection<StationViewModel> Stations { get; set; } = new();

        /// <summary>
        /// 弹窗开关状态
        /// </summary>
        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set { _isPopupOpen = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 弹窗标题（显示门名称）
        /// </summary>
        public string PopupTitle
        {
            get => _popupTitle;
            set { _popupTitle = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 当前选中的门（用于弹窗显示详情）
        /// </summary>
        public DoorModel? SelectedDoor
        {
            get => _selectedDoor;
            set
            {
                _selectedDoor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AlarmBits));
                OnPropertyChanged(nameof(StatusBits));
                OnPropertyChanged(nameof(ActiveAlarmCount));
                OnPropertyChanged(nameof(ActiveStatusCount));
            }
        }

        /// <summary>
        /// 弹窗告警点位（从选中门的Bits筛选）
        /// </summary>
        public ObservableCollection<DoorBitConfig> AlarmBits
        {
            get
            {
                if (SelectedDoor == null) return new();
                // 筛选告警类点位（优先级配置在 HeaderPriority/ImagePriority/BottomPriority 中）
                var alarms = SelectedDoor.Bits.Where(b =>
                    b.HeaderPriority > 0 || b.ImagePriority > 0 || b.BottomPriority > 0)
                    .OrderBy(b => b.SortOrder)
                    .ToList();
                return new ObservableCollection<DoorBitConfig>(alarms);
            }
        }

        /// <summary>
        /// 激活的告警点位数量（BitValue = true）
        /// </summary>
        public int ActiveAlarmCount
        {
            get
            {
                if (SelectedDoor == null) return 0;
                return SelectedDoor.Bits.Count(b =>
                    (b.HeaderPriority > 0 || b.ImagePriority > 0 || b.BottomPriority > 0) &&
                    b.BitValue == true);
            }
        }

        /// <summary>
        /// 弹窗状态点位（从选中门的Bits筛选）
        /// </summary>
        public ObservableCollection<DoorBitConfig> StatusBits
        {
            get
            {
                if (SelectedDoor == null) return new();
                // 筛选状态类点位
                var status = SelectedDoor.Bits.Where(b =>
                    b.HeaderPriority == 0 && b.ImagePriority == 0 && b.BottomPriority == 0)
                    .OrderBy(b => b.SortOrder)
                    .ToList();
                return new ObservableCollection<DoorBitConfig>(status);
            }
        }

        /// <summary>
        /// 激活的状态点位数量（BitValue = true）
        /// </summary>
        public int ActiveStatusCount
        {
            get
            {
                if (SelectedDoor == null) return 0;
                return SelectedDoor.Bits.Count(b =>
                    b.HeaderPriority == 0 && b.ImagePriority == 0 && b.BottomPriority == 0 &&
                    b.BitValue == true);
            }
        }

        #endregion

        #region 命令

        public ICommand ClosePopupCommand { get; set; }
        public ICommand OpenDoorDetailCommand { get; set; }

        #endregion

        #region 构造函数

        public MainViewModel()
        {
            // 初始化命令
            ClosePopupCommand = new RelayCommand(OnClosePopup);
            OpenDoorDetailCommand = new RelayCommand(OnOpenDoorDetail);

            // 加载站台数据（从配置文件或数据库加载）
            LoadStations();

            // 启动数据更新循环
            _ = Task.Run(UpdateLoop, _updateLoopTokenSource.Token);
        }

        #endregion

        #region 数据加载

        /// <summary>
        /// 从数据库加载站台数据
        /// </summary>
        private void LoadStations()
        {
            try
            {
                // 检查数据库配置是否存在
                if (GlobalData.SysCfg == null)
                {
                    Debug.WriteLine("数据库配置未初始化，无法加载站台数据");
                    return;
                }

                // 构建连接字符串
                string connectionString = $"Server={GlobalData.SysCfg.ServerAddress};" +
                                        $"Database={GlobalData.SysCfg.DatabaseName};" +
                                        $"User ID={GlobalData.SysCfg.UserName};" +
                                        $"Password={GlobalData.SysCfg.UserPassword};" +
                                        $"CharSet=utf8mb4;";

                // 创建数据服务并加载站台
                var dataService = new StationDataService(connectionString);
                var stationList = dataService.LoadAllStations();

                // 清空现有数据
                Stations.Clear();

                // 添加到视图模型集合
                foreach (var station in stationList)
                {
                    Stations.Add(new StationViewModel(station));
                }

                Debug.WriteLine($"成功从数据库加载 {Stations.Count} 个站台");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载站台数据失败: {ex.Message}");
                Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }

        #endregion

        #region 数据更新循环

        /// <summary>
        /// 主更新循环：持续刷新门和面板状态
        /// </summary>
        private async Task UpdateLoop()
        {
            var token = _updateLoopTokenSource.Token;

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(300, token);

                // TODO: 从 PLC/设备点位更新门和面板状态
                // 遍历所有站台 -> 门组 -> 门 -> 点位，更新 BitValue
                // 然后根据优先级裁决，更新 DoorVisualResult

                try
                {
                    SafeInvoke(() =>
                    {
                        // 示例：更新门的视觉状态
                        foreach (var station in Stations)
                        {
                            foreach (var doorGroup in station.Station.DoorGroups)
                            {
                                foreach (var door in doorGroup.Doors)
                                {
                                    // TODO: 根据点位值裁决门的显示状态
                                    // UpdateDoorVisual(door);
                                }
                            }

                            foreach (var panelGroup in station.Station.PanelGroups)
                            {
                                foreach (var panel in panelGroup.Panels)
                                {
                                    // TODO: 更新面板点位值
                                    // UpdatePanelBits(panel);
                                }
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UpdateLoop Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// UI 线程安全调用
        /// </summary>
        private static void SafeInvoke(Action action)
        {
            try
            {
                if (Application.Current?.Dispatcher != null &&
                    !Application.Current.Dispatcher.HasShutdownStarted)
                {
                    Application.Current.Dispatcher.Invoke(action);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dispatcher Invoke Failed: {ex.Message}");
            }
        }

        #endregion

        #region 命令处理

        /// <summary>
        /// 关闭弹窗
        /// </summary>
        private void OnClosePopup(object obj)
        {
            IsPopupOpen = false;
            SelectedDoor = null;
        }

        /// <summary>
        /// 打开门详情弹窗
        /// </summary>
        private void OnOpenDoorDetail(object obj)
        {
            if (obj is DoorModel door)
            {
                SelectedDoor = door;
                PopupTitle = $"门 {door.DoorName} - 详细状态";
                IsPopupOpen = true;
            }
        }

        #endregion

        #region 资源释放

        public void Dispose()
        {
            _updateLoopTokenSource?.Cancel();
            _updateLoopTokenSource?.Dispose();
        }

        #endregion
    }
}
