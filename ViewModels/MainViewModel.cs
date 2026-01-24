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
using System.Windows.Media;
using System.ComponentModel;
using ControlLibrary.Models;

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
                if (_selectedDoor != null)
                {
                    foreach (var b in _selectedDoor.Bits) b.PropertyChanged -= Bit_PropertyChanged;
                }

                _selectedDoor = value;

                if (_selectedDoor != null)
                {
                    foreach (var b in _selectedDoor.Bits) b.PropertyChanged += Bit_PropertyChanged;
                }

                OnPropertyChanged();
                
                // 缓存并通知
                RefreshCategoryGroups();
                NotifyAggregateProperties();
            }
        }

        private void Bit_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DoorBitConfig.BitValue))
            {
                NotifyAggregateProperties();
            }
        }

        private void NotifyAggregateProperties()
        {
            OnPropertyChanged(nameof(ActiveAlarmCount));
            OnPropertyChanged(nameof(ActiveStatusCount));
            OnPropertyChanged(nameof(AlarmBits));
            OnPropertyChanged(nameof(StatusBits));
            // CategoryGroups 内部已经监听了位变化，所以不需要通知集合本身变化
            // 但如果使用的是实时计算属性，也需要通知
            OnPropertyChanged(nameof(CategoryGroups)); 
        }

        private ObservableCollection<CategoryGroup> _categoryGroups = new();
        /// <summary>
        /// 按分类分组的点位集合（用于弹窗动态显示）
        /// </summary>
        public ObservableCollection<CategoryGroup> CategoryGroups
        {
            get => _categoryGroups;
            private set { _categoryGroups = value; OnPropertyChanged(); }
        }

        private void RefreshCategoryGroups()
        {
            if (SelectedDoor == null)
            {
                CategoryGroups = new();
                return;
            }

            // 按 CategoryId 分组点位
            var list = SelectedDoor.Bits
                .Where(b => b.Category != null)
                .OrderBy(b => b.SortOrder)
                .GroupBy(b => b.CategoryId)
                .Select(g => new CategoryGroup
                {
                    Category = g.First().Category!,
                    Bits = new ObservableCollection<DoorBitConfig>(g.OrderBy(b => b.SortOrder))
                })
                .OrderBy(cg => cg.Category.SortOrder)
                .ToList();

            // 如果有未分类的点位
            var uncategorized = SelectedDoor.Bits
                .Where(b => b.Category == null)
                .OrderBy(b => b.SortOrder)
                .ToList();

            if (uncategorized.Any())
            {
                list.Add(new CategoryGroup
                {
                    Category = new BitCategoryModel
                    {
                        CategoryId = 0,
                        Name = "其它",
                        Icon = "📋",
                        BackgroundColor = "#607D8B",
                        ForegroundColor = "#FFFFFF",
                        SortOrder = 999
                    },
                    Bits = new ObservableCollection<DoorBitConfig>(uncategorized)
                });
            }

            CategoryGroups = new ObservableCollection<CategoryGroup>(list);
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
            // 将当前实例注册到全局，以便通讯服务更新
            GlobalData.MainVm = this;

            // 初始化命令
            ClosePopupCommand = new RelayCommand(OnClosePopup);
            OpenDoorDetailCommand = new RelayCommand(OnOpenDoorDetail);

            // 异步加载业务数据 (站台/门/点位)
            _ = LoadDataAsync();

            // 启动数据更新循环
            _ = Task.Run(UpdateLoop, _updateLoopTokenSource.Token);
        }

        private async Task LoadDataAsync()
        {
            await DataManager.Instance.LoadBusinessDataAsync();

            // 注入命令（避免 XAML 绑定时的 RelativeSource 查找）
            foreach (var station in Stations)
            {
                if (station.Station == null) continue;
                foreach (var group in station.Station.DoorGroups)
                {
                    foreach (var door in group.Doors)
                    {
                        door.OpenDetailCommand = OpenDoorDetailCommand;
                    }
                }
            }

            Debug.WriteLine($"[MainVM] Data loading completed. Status: {Stations.Count} stations.");
        }

        #endregion

        #region 数据加载

        /// <summary>
        /// 从数据库加载站台数据
        /// </summary>
        private void LoadStations()
        {
            // 旧逻辑已由 DataManager.LoadBusinessDataAsync 接管
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
                                    UpdateDoorVisual(door);
                                }
                            }

                            foreach (var panelGroup in station.Station.PanelGroups)
                            {
                                foreach (var panel in panelGroup.Panels)
                                {
                                    UpdatePanelVisual(panel);
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
        /// 基于优先级裁决门的最终视觉状态
        /// </summary>
        private void UpdateDoorVisual(DoorModel door)
        {
            // 业务逻辑下沉到 DataManager
            DataManager.Instance.AdjudicateDoorVisual(door);
        }

        private void UpdatePanelVisual(PanelModel panel)
        {
            // 面板点位目前由其内部 PanelBitConfig.BitValue 驱动 DisplayBrush
            // 这里暂不需要复杂的裁决逻辑
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
