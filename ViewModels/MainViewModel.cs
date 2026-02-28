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
using System.Windows.Threading;
using ControlLibrary.Models;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 主界面视图模型 (UI 数据驱动核心)
    /// 管理所有站台 (Stations)、门详情 (SelectedDoor) 以及弹窗逻辑。
    /// 这里的业务逻辑尽量下沉到 DataManager，保持 VM 轻量化。
    /// </summary>
    public class MainViewModel : NotifyPropertyChanged, IDisposable
    {
        #region Fields (字段)

        private CancellationTokenSource _updateLoopTokenSource = new();
        private bool _isPopupOpen;
        private string _popupTitle = "";
        private DoorModel? _selectedDoor;
        private ObservableCollection<CategoryGroup> _categoryGroups = new();
        private readonly DispatcherTimer _aggregateUpdateTimer;
        private int _aggregateUpdatePending = 0;
        private bool _disposed = false;

        // 缓存字段：避免每次 UI 绑定读取时重建集合（防止频繁 GC 压力）
        private ObservableCollection<DoorBitConfig> _alarmBitsCache = new();
        private ObservableCollection<DoorBitConfig> _statusBitsCache = new();

        #endregion

        #region Properties (核心属性)

        /// <summary>
        /// 站台视图模型集合 (数据源)
        /// UI 的 ItemsControl 直接绑定此集合，自动渲染所有站台卡片。
        /// </summary>
        public ObservableCollection<StationViewModel> Stations { get; set; } = new();

        /// <summary>
        /// 详情弹窗是否打开
        /// </summary>
        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set
            {
                if (_isPopupOpen == value) return;
                _isPopupOpen = value;

                // 弹窗关闭时，主界面仅需要门聚合视觉，禁用门点位逐条通知以降低 CPU。
                DoorBitConfig.SuppressBitValueNotifications = !value;

                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 弹窗标题 (通常显示: 站台名 - 门名称)
        /// </summary>
        public string PopupTitle
        {
            get => _popupTitle;
            set { _popupTitle = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 当前选中的门 (用于弹窗显示详情数据)
        /// 设置此属性会自动触发：
        /// 1. 订阅新门各位的 PropertyChanged 事件 (以便实时刷新聚合统计)
        /// 2. 刷新分类组 (CategoryGroups)
        /// 3. 通知聚合属性变更 (ActiveAlarmCount, ActiveStatusCount)
        /// </summary>
        public DoorModel? SelectedDoor
        {
            get => _selectedDoor;
            set
            {
                if (_selectedDoor != null) // 移除旧订阅
                {
                    foreach (var b in _selectedDoor.Bits) b.PropertyChanged -= Bit_PropertyChanged;
                }

                _selectedDoor = value;

                if (_selectedDoor != null) // 添加新订阅
                {
                    foreach (var b in _selectedDoor.Bits) b.PropertyChanged += Bit_PropertyChanged;
                }

                OnPropertyChanged();
                
                // 收到新门后，立即刷新衍生数据
                RefreshCategoryGroups();
                NotifyAggregateProperties();
            }
        }

        /// <summary>
        /// 按分类分组的点位集合 (用于弹窗中的列表动态展示)
        /// 包含：报警、状态、其它等分组
        /// </summary>
        public ObservableCollection<CategoryGroup> CategoryGroups
        {
            get => _categoryGroups;
            private set { _categoryGroups = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 报警类别的点位集合 (兼容旧UI绑定) — 缓存版，仅在门切换时刷新
        /// </summary>
        public ObservableCollection<DoorBitConfig> AlarmBits => _alarmBitsCache;

        /// <summary>
        /// 状态类别的点位集合 (兼容旧UI绑定) — 缓存版，仅在门切换时刷新
        /// </summary>
        public ObservableCollection<DoorBitConfig> StatusBits => _statusBitsCache;

        /// <summary>
        /// 当前激活的报警数量 (红色徽标计数)
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
        /// 当前激活的状态数量 (蓝色徽标计数)
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

        #region Commands (命令)

        /// <summary>
        /// 关闭弹窗命令
        /// </summary>
        public ICommand ClosePopupCommand { get; set; }
        
        /// <summary>
        /// 打开门详情命令 (参数: DoorModel)
        /// </summary>
        public ICommand OpenDoorDetailCommand { get; set; }

        /// <summary>
        /// 刷新 UI 数据命令
        /// </summary>
        public ICommand RefreshCommand { get; set; }

        #endregion

        #region Constructor (构造函数)

        public MainViewModel()
        {
            _aggregateUpdateTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _aggregateUpdateTimer.Tick += (_, __) =>
            {
                _aggregateUpdateTimer.Stop();
                System.Threading.Interlocked.Exchange(ref _aggregateUpdatePending, 0);
                RaiseAggregateProperties();
            };

            // 将当前实例注册到全局，以便通讯服务更新 UI
            GlobalData.MainVm = this;

            // 初始化命令
            ClosePopupCommand = new RelayCommand(OnClosePopup);
            OpenDoorDetailCommand = new RelayCommand(OnOpenDoorDetail);
            RefreshCommand = new RelayCommand(async _ => await LoadDataAsync());

            // 异步加载业务数据 (站台/门/点位)
            _ = LoadDataAsync();

            // 启动数据更新循环 (目前主要是心跳保活)
            _ = Task.Run(UpdateLoop, _updateLoopTokenSource.Token);
        }

        #endregion

        #region Methods (逻辑方法)

        /// <summary>
        /// 监控点位值变化事件
        /// 当详情页打开时，任何点位的数值变化都会触发此回调，进而更新统计数字
        /// </summary>
        private void Bit_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DoorBitConfig.BitValue))
            {
                NotifyAggregateProperties();
            }
        }

        /// <summary>
        /// 批量通知聚合属性更新
        /// </summary>
        private void NotifyAggregateProperties()
        {
            if (System.Threading.Interlocked.Exchange(ref _aggregateUpdatePending, 1) == 1)
            {
                return; // 已有待处理更新，避免高频刷 UI
            }

            // 使用 SafeInvoke 确保 UI 线程安全 (虽然 NotifyPropertyChanged 通常会自动 marshal，但聚合计算最好明确)
            SafeInvoke(() =>
            {
                if (!_aggregateUpdateTimer.IsEnabled)
                {
                    _aggregateUpdateTimer.Start();
                }
            });
        }

        private void RaiseAggregateProperties()
        {
            OnPropertyChanged(nameof(ActiveAlarmCount));
            OnPropertyChanged(nameof(ActiveStatusCount));
            OnPropertyChanged(nameof(AlarmBits)); // Notify that the content of the cached collection might have changed
            OnPropertyChanged(nameof(StatusBits)); // Notify that the content of the cached collection might have changed
            // CategoryGroups 内部集合元素变化不需要通知 CategoryGroups 本身，但如果在这里重新分组则需要
            // 目前是只更新数字，CategoryGroups 结构不变
        }

        /// <summary>
        /// 重新构建分类组
        /// 根据 SelectedDoor 的点位配置，动态生成用于弹窗展示的分组列表
        /// </summary>
        private void RefreshCategoryGroups()
        {
            if (SelectedDoor == null)
            {
                CategoryGroups = new();
                _alarmBitsCache.Clear();
                _statusBitsCache.Clear();
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

            // 如果有未分类的点位，归入 "其它"
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

            // 更新缓存的 AlarmBits 和 StatusBits
            _alarmBitsCache.Clear();
            foreach (var bit in SelectedDoor.Bits.Where(b => b.Category != null && b.Category.Code == "Alarm").OrderBy(b => b.SortOrder))
            {
                _alarmBitsCache.Add(bit);
            }

            _statusBitsCache.Clear();
            foreach (var bit in SelectedDoor.Bits.Where(b => b.Category != null && b.Category.Code == "Status").OrderBy(b => b.SortOrder))
            {
                _statusBitsCache.Add(bit);
            }
        }

        /// <summary>
        /// 异步加载业务数据
        /// </summary>
        private async Task LoadDataAsync()
        {
            await DataManager.Instance.LoadBusinessDataAsync();

            // 注入命令逻辑已被 View 层 RelativeSource 替代，此处不再通过代码注入
            // foreach (var station in Stations) ...

            Debug.WriteLine($"[MainVM] Data loading completed. Status: {Stations.Count} stations.");
        }

        /// <summary>
        /// 主更新循环
        /// 业务逻辑已下沉到 DataManager，由通讯层事件驱动更新，不再需要轮询扫描。
        /// 保留此循环仅作为心跳检测。
        /// </summary>
        private async Task UpdateLoop()
        {
            var token = _updateLoopTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(10000, token); // 10秒心跳
            }
        }

        /// <summary>
        /// UI 线程安全调用辅助方法
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

        /// <summary>
        /// 关闭弹窗逻辑
        /// </summary>
        private void OnClosePopup(object obj)
        {
            IsPopupOpen = false;
            SelectedDoor = null;
        }

        /// <summary>
        /// 打开门详情弹窗逻辑
        /// </summary>
        private void OnOpenDoorDetail(object obj)
        {
            if (obj is DoorModel door)
            {
                SelectedDoor = door;

                // 构建完整的标题：站台名称 - 门名称 - 详细信息
                string stationName = "";

                // 从 Stations 集合中查找包含该门的站台 (反向查找)
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

        /// <summary>
        /// 释放资源（IDisposable 实现）
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 清理当前门的事件订阅，防止悬挂引用
            if (_selectedDoor != null)
            {
                foreach (var b in _selectedDoor.Bits)
                    b.PropertyChanged -= Bit_PropertyChanged;
                _selectedDoor = null;
            }

            _aggregateUpdateTimer?.Stop();
            _updateLoopTokenSource?.Cancel();
            _updateLoopTokenSource?.Dispose();
        }

        #endregion
    }
}
