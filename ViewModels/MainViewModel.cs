using Base;
using DoorMonitorSystem.Assets.Services;
using DoorMonitorSystem.Assets.Helper;
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
using System.Collections.Generic;

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
                // 通知所有相关属性更新
                OnPropertyChanged(nameof(CategoryGroups));
                OnPropertyChanged(nameof(AlarmBits));
                OnPropertyChanged(nameof(StatusBits));
                OnPropertyChanged(nameof(ActiveAlarmCount));
                OnPropertyChanged(nameof(ActiveStatusCount));
            }
        }

        /// <summary>
        /// 按分类分组的点位集合（用于弹窗动态显示）
        /// </summary>
        public ObservableCollection<CategoryGroup> CategoryGroups
        {
            get
            {
                if (SelectedDoor == null) return new();

                var groups = new ObservableCollection<CategoryGroup>();

                // 按分类分组点位
                var categoryGrouping = SelectedDoor.Bits
                    .Where(b => b.Category != null)
                    .GroupBy(b => b.Category)
                    .OrderBy(g => g.Key.SortOrder);

                foreach (var group in categoryGrouping)
                {
                    var category = group.Key;
                    var bits = new ObservableCollection<DoorBitConfig>(
                        group.OrderBy(b => b.SortOrder)
                    );

                    // 计算激活数量
                    int activeCount = bits.Count(b => b.BitValue == true);

                    groups.Add(new CategoryGroup
                    {
                        Category = category,
                        Bits = bits,
                        ActiveCount = activeCount
                    });
                }

                // 添加未分类的点位（如果有）
                var uncategorized = SelectedDoor.Bits
                    .Where(b => b.Category == null)
                    .OrderBy(b => b.SortOrder)
                    .ToList();

                if (uncategorized.Any())
                {
                    groups.Add(new CategoryGroup
                    {
                        Category = new BitCategoryModel
                        {
                            CategoryId = 0,
                            Code = "Uncategorized",
                            Name = "其他",
                            Icon = "📋",
                            BackgroundColor = "#607D8B",
                            ForegroundColor = "#FFFFFF",
                            SortOrder = 999
                        },
                        Bits = new ObservableCollection<DoorBitConfig>(uncategorized),
                        ActiveCount = uncategorized.Count(b => b.BitValue == true)
                    });
                }

                return groups;
            }
        }

        /// <summary>
        /// 报警类别的点位集合（用于UI绑定）
        /// </summary>
        public ObservableCollection<DoorBitConfig> AlarmBits
        {
            get
            {
                if (SelectedDoor == null) return new();

                return new ObservableCollection<DoorBitConfig>(
                    SelectedDoor.Bits
                        .Where(b => b.Category != null && b.Category.Code == "Alarm")
                        .OrderBy(b => b.SortOrder)
                );
            }
        }

        /// <summary>
        /// 状态类别的点位集合（用于UI绑定）
        /// </summary>
        public ObservableCollection<DoorBitConfig> StatusBits
        {
            get
            {
                if (SelectedDoor == null) return new();

                return new ObservableCollection<DoorBitConfig>(
                    SelectedDoor.Bits
                        .Where(b => b.Category != null && b.Category.Code == "Status")
                        .OrderBy(b => b.SortOrder)
                );
            }
        }

        /// <summary>
        /// 激活的报警数量
        /// </summary>
        public int ActiveAlarmCount
        {
            get
            {
                if (SelectedDoor == null) return 0;
                return SelectedDoor.Bits
                    .Count(b => b.Category != null &&
                                b.Category.Code == "Alarm" &&
                                b.BitValue == true);
            }
        }

        /// <summary>
        /// 激活的状态数量
        /// </summary>
        public int ActiveStatusCount
        {
            get
            {
                if (SelectedDoor == null) return 0;
                return SelectedDoor.Bits
                    .Count(b => b.Category != null &&
                                b.Category.Code == "Status" &&
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

                // 构建完整的标题：站台名称 - 门名称 - 详细信息
                string stationName = "";

                // 从 Stations 集合中查找包含该门的站台
                foreach (var station in Stations)
                {
                    bool foundDoor = false;
                    foreach (var doorGroup in station.Station.DoorGroups)
                    {
                        if (doorGroup.Doors.Contains(door))
                        {
                            stationName = station.Station.StationName;
                            foundDoor = true;
                            break;
                        }
                    }
                    if (foundDoor) break;
                }

                PopupTitle = $"{stationName} - {door.DoorName} - 详细信息";
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
