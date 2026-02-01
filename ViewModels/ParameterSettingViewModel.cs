using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Base;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Assets.Services;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 参数设置页面的主视图模型
    /// 作为一个容器 VM，它动态监听系统站台配置，并从数据库加载底层 ASD 映射规则。
    /// </summary>
    public class ParameterSettingViewModel : NotifyPropertyChanged
    {
        #region 字段与属性

        private ObservableCollection<StationParameterViewModel> _stationSettings;
        private readonly ObservableCollection<AsdModelMapping> _asdModels;

        /// <summary>
        /// 所有站台对应的参数配置单元集合
        /// </summary>
        public ObservableCollection<StationParameterViewModel> StationSettings
        {
            get => _stationSettings;
            set { _stationSettings = value; OnPropertyChanged(); }
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public ParameterSettingViewModel()
        {
            StationSettings = new ObservableCollection<StationParameterViewModel>();
            
            // 1. 从数据库读取 ASD 型号映射规则，实现动态扩展
            var modelEntities = DataManager.Instance.LoadAsdModelsFromDb();
            _asdModels = new ObservableCollection<AsdModelMapping>(
                modelEntities.Select(e => new AsdModelMapping { DisplayName = e.DisplayName, PlcId = e.PlcId })
            );

            // 如果数据库为空（理论上 DataManager 会初始化，但此处做防御），提供基础 fallback
            if (_asdModels.Count == 0)
            {
                _asdModels.Add(new AsdModelMapping { DisplayName = "ASD_DEFAULT", PlcId = 1 });
            }

            // 2. 初始同步：尝试从 MainViewModel 抓取当前已有的站台
            SyncStations();

            // 3. 动态监听全局站台列表变更
            if (GlobalData.MainVm?.Stations != null)
            {
                GlobalData.MainVm.Stations.CollectionChanged += OnGlobalStationsChanged;
            }
        }

        /// <summary>
        /// 监听全局站台列表的变化
        /// </summary>
        private void OnGlobalStationsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            SyncStations();
        }

        /// <summary>
        /// 站台列表精准同步逻辑
        /// </summary>
        private void SyncStations()
        {
            if (GlobalData.MainVm?.Stations == null) return;

            var currentStations = GlobalData.MainVm.Stations
                                       .Where(s => s.Station != null)
                                       .ToList();

            // 更新或添加新站台
            foreach (var stVm in currentStations)
            {
                var name = stVm.Station.StationName;
                int stationId = stVm.Station.StationId;

                // 尝试解析该站台关联的实际物理设备ID
                int? devId = ResolveDeviceId(name);

                var existing = StationSettings.FirstOrDefault(x => x.StationName == name);
                if (existing == null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        StationSettings.Add(new StationParameterViewModel(name, _asdModels, devId, stationId));
                    });
                }
                else
                {
                    // 同步 ID (解决异步加载或配置变更)
                    existing.TargetDeviceId = devId;
                    existing.StationId = stationId;
                }
            }

            // 移除已删除站台
            var toRemove = StationSettings.Where(s => !currentStations.Any(x => x.Station.StationName == s.StationName)).ToList();
            foreach (var item in toRemove)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    StationSettings.Remove(item);
                });
            }

            // 防控与兜底逻辑
            if (StationSettings.Count == 0 && currentStations.Count == 0)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    if (!StationSettings.Any(x => x.StationName == "通用 ASD 节点"))
                    {
                        StationSettings.Add(new StationParameterViewModel("通用 ASD 节点", _asdModels));
                    }
                });
            }
            else if (StationSettings.Count > 0 && currentStations.Count > 0)
            {
                var dummy = StationSettings.FirstOrDefault(x => x.StationName == "通用 ASD 节点");
                if (dummy != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => StationSettings.Remove(dummy));
                }
            }
        }

        /// <summary>
        /// 智能解析站台对应的 DeviceID
        /// </summary>
        private int? ResolveDeviceId(string stationName)
        {
            try
            {
                // 根据站台名称反查绑定的 ViewModel 实例
                var stVm = GlobalData.MainVm?.Stations.FirstOrDefault(s => s.Station?.StationName == stationName);
                if (stVm == null) return null;

                // 策略 1：尝试从该站台下的“门/屏蔽门”配置中提取关联的设备ID。
                // 这适用于那些已经配置了门状态监控的站台。
                var firstDoor = stVm.Station.DoorGroups.FirstOrDefault()?.Doors.FirstOrDefault();
                if (firstDoor != null && firstDoor.Bits.Count > 0)
                {
                    int bitId = firstDoor.Bits[0].BitId;
                    var cfg = DeviceCommunicationService.Instance?.GetPointConfigById(bitId);
                    if (cfg?.SourceDeviceId > 0) return cfg.SourceDeviceId;
                }

                // 策略 2：回退逻辑。如果该站台还没配门（例如仅作为参数集），
                // 则尝试提取该站台 ID 下已配置的任意点位的 DeviceId。
                var stationId = stVm.Station.StationId;
                var stationPoints = DeviceCommunicationService.Instance?.GetPointConfigsForStation(stationId);
                var firstPoint = stationPoints?.FirstOrDefault();
                if (firstPoint != null) return firstPoint.SourceDeviceId;
            }
            catch(Exception ex) 
            {
                Assets.Helper.LogHelper.Warn($"[ParamSetting] 设备 ID 解析异常 ({stationName}): {ex.Message}");
            }
            return null;
        }
    }
}
