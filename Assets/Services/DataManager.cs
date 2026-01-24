using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ControlLibrary.Models;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.ViewModels;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.system;
using Communicationlib.config;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 全局数据管理中心 (Data Brain)
    /// 负责：
    /// 1. 加载所有配置文件 (JSON/DB)
    /// 2. 维护点位运行时状态与缓存
    /// 3. 执行业务逻辑裁决 (Visual Adjudication)
    /// 4. 驱动 UI 与通讯层的业务同步
    /// </summary>
    public class DataManager
    {
        private static readonly Lazy<DataManager> _instance = new(() => new DataManager());
        public static DataManager Instance => _instance.Value;

        // 点位索引缓存，提高通讯更新效率
        private readonly Dictionary<string, DoorBitConfig> _doorBitCache = new();
        private readonly Dictionary<string, PanelBitConfig> _panelBitCache = new();
        // 门模型缓存，用于快速查找并刷新视觉裁决
        private readonly Dictionary<int, DoorModel> _doorCache = new();
        
        private bool _isCacheBuilt = false;
        private bool _debugKeysPrinted = false;

        // 交互优先模式：当用户拖动弹窗时，暂停数据刷新以保证流畅度
        public volatile bool IsUIInteractionActive = false;

        private DataManager() { }

        /// <summary>
        /// 第一阶段初始化：加载基础配置文件
        /// 应该在程序启动最早期调用 (MainWindow 构造函数)
        /// </summary>
        public void Initialize()
        {
            try
            {
                LogHelper.Info("[DataManager] Static config initialization started.");
                LoadSystemConfig();
                LoadGraphicDictionary();
                LoadDevices();
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] Initialization Failed", ex);
            }
        }

        /// <summary>
        /// 第二阶段初始化：加载业务数据 (站台/门/面板)
        /// 应该在 MainViewModel 准备好后调用
        /// </summary>
        public async Task LoadBusinessDataAsync()
        {
            try
            {
                if (GlobalData.SysCfg == null) return;

                string connStr = $"Server={GlobalData.SysCfg.ServerAddress};" +
                               $"Database={GlobalData.SysCfg.DatabaseName};" +
                               $"User ID={GlobalData.SysCfg.UserName};" +
                               $"Password={GlobalData.SysCfg.UserPassword};" +
                               $"CharSet=utf8mb4;";

                var stationService = new StationDataService(connStr);
                var stations = await Task.Run(() => stationService.LoadAllStations());

                if (GlobalData.MainVm != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        GlobalData.MainVm.Stations.Clear();
                        foreach (var s in stations)
                        {
                            GlobalData.MainVm.Stations.Add(new StationViewModel(s));
                        }
                    });
                    
                    BuildBitCache();
                    LogHelper.Info($"[DataManager] Business data loaded. Stations: {stations.Count}");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error("[DataManager] LoadBusinessDataAsync Failed", ex);
            }
        }

        #region 配置加载逻辑 (JSON)

        private void LoadSystemConfig()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "SystemConfig.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                GlobalData.SysCfg = JsonSerializer.Deserialize<SysCfg>(json);
                LogHelper.Info("[DataManager] SystemConfig loaded.");
            }
        }

        private void LoadGraphicDictionary()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "GraphicDictionary.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var imported = JsonSerializer.Deserialize<List<SerializableGraphicGroup>>(json);
                if (imported != null)
                {
                    GlobalData.GraphicDictionary?.Clear();
                    foreach (var group in imported)
                    {
                        var itemList = new List<IconItem>();
                        foreach (var item in group.Items)
                        {
                            var geometry = Geometry.Parse(item.PathData);
                            if (geometry.CanFreeze) geometry.Freeze();

                            var stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.StrokeColor));
                            if (stroke.CanFreeze) stroke.Freeze();

                            var fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.FillColor));
                            if (fill.CanFreeze) fill.Freeze();

                            itemList.Add(new IconItem
                            {
                                Data = geometry,
                                Stroke = stroke,
                                Fill = fill,
                                StrokeThickness = item.StrokeThickness
                            });
                        }
                        GlobalData.GraphicDictionary[group.Name] = itemList;
                    }
                    var keys = string.Join(", ", GlobalData.GraphicDictionary.Keys);
                    LogHelper.Info($"[DataManager] GraphicDictionary loaded. Keys: [{keys}]");
                }
            }
        }

        private void LoadDevices()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "devices.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                GlobalData.ListDveices = JsonSerializer.Deserialize<List<ConfigEntity>>(json) ?? new List<ConfigEntity>();
                LogHelper.Info($"[DataManager] Devices loaded: {GlobalData.ListDveices.Count} devices.");
            }
        }

        #endregion

        #region 运行时点位管理

        /// <summary>
        /// 通用点位值更新入口
        /// </summary>
        // UI批量更新队列
        private readonly System.Collections.Concurrent.ConcurrentQueue<(int targetObjId, int bitConfigId, TargetType targetType, bool value)> _uiUpdateQueue = new();
        private bool _isBatchLoopRunning = false;

        /// <summary>
        /// 通用点位值更新入口（改为批量缓冲模式）
        /// </summary>
        public void UpdatePointValue(int targetObjId, int bitConfigId, TargetType targetType, bool value)
        {
            if (!_isCacheBuilt) BuildBitCache();

            // 将更新请求加入队列，不再直接Invoke
            _uiUpdateQueue.Enqueue((targetObjId, bitConfigId, targetType, value));

            if (!_isBatchLoopRunning)
            {
                StartBatchUpdateLoop();
            }
        }


        private void StartBatchUpdateLoop()
        {
            _isBatchLoopRunning = true;
            Task.Run(async () =>
            {
                while (true) // 简化的生命周期管理，实际应用需结合Dispose
                {
                    try
                    {
                        if (_uiUpdateQueue.IsEmpty)
                        {
                            await Task.Delay(50); // 空闲时等待
                            continue;
                        }

                        // 取出当前队列中所有待更新项（批量处理）
                        var batch = new List<(int targetObjId, int bitConfigId, TargetType targetType, bool value)>();
                        // 限制单次批处理数量，防止单帧耗时过长导致界面卡顿 (拖动发飘)
                        // 每次最多处理 200 个状态变化，剩余的留到下一帧
                        while (batch.Count < 200 && _uiUpdateQueue.TryDequeue(out var item))
                        {
                            batch.Add(item);
                        }

                        // 一次性调度到 UI 线程
                        // 关键修改：使用 InvokeAsync + Background 优先级
                        // 能够确保 鼠标输入(Input) 和 渲染(Render) 优先于 数据更新，解决"漂移/不跟手"问题
                        if (batch.Count > 0 && Application.Current != null && Application.Current.Dispatcher != null)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                var dirtyDoors = new HashSet<DoorModel>();

                                foreach (var (targetObjId, bitConfigId, targetType, value) in batch)
                                {
                                    string key = $"{targetObjId}_{bitConfigId}";
                                    
                                    if (targetType == TargetType.Door)
                                    {
                                        if (_doorBitCache.TryGetValue(key, out var bit))
                                        {
                                            bit.BitValue = value;
                                            
                                            // 标记该门需要重新裁决视觉状态
                                            if (_doorCache.TryGetValue(targetObjId, out var door))
                                            {
                                                dirtyDoors.Add(door);
                                            }
                                        }
                                    }
                                    else if (targetType == TargetType.Panel)
                                    {
                                        if (_panelBitCache.TryGetValue(key, out var bit))
                                        {
                                            bit.BitValue = value;
                                        }
                                    }
                                }

                                // 批量刷新脏门（裁决优先级，更新图标）
                                foreach (var door in dirtyDoors)
                                {
                                    AdjudicateDoorVisual(door);
                                }

                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DataManager] Batch Update Error: {ex.Message}");
                    }
                    
                    // 控制刷新频率: 30FPS (33ms/帧)
                    // 既保证视觉流畅，又留出足够的空闲时间给 UI 响应鼠标事件
                    await Task.Delay(33);
                }
            });
        }

        /// <summary>
        /// 为所有运行时点位构建快速索引
        /// </summary>
        public void BuildBitCache()
        {
            if (GlobalData.MainVm == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                _doorBitCache.Clear();
                _panelBitCache.Clear();
                _doorCache.Clear();

                foreach (var stationVm in GlobalData.MainVm.Stations)
                {
                    if (stationVm.Station == null) continue;

                    foreach (var doorGroup in stationVm.Station.DoorGroups)
                    {
                        foreach (var door in doorGroup.Doors)
                        {
                            _doorCache[door.DoorId] = door; // 缓存门模型
                            foreach (var bit in door.Bits)
                            {
                                _doorBitCache[$"{door.DoorId}_{bit.BitId}"] = bit;
                            }
                        }
                    }

                    foreach (var panelGroup in stationVm.Station.PanelGroups)
                    {
                        foreach (var panel in panelGroup.Panels)
                        {
                            foreach (var bit in panel.BitList)
                            {
                                _panelBitCache[$"{panel.PanelId}_{bit.BitId}"] = bit;
                            }
                        }
                    }
                }
                _isCacheBuilt = true;
                LogHelper.Info($"[DataManager] BitCache Built. DoorBits={_doorBitCache.Count}, Doors={_doorCache.Count}");
            });
        }

        #endregion

        #region 业务逻辑裁决 (Visual Adjudication)

        /// <summary>
        /// 基于优先级裁决门的所有视觉要素
        /// </summary>
        public void AdjudicateDoorVisual(DoorModel door)
        {
            if (door?.Bits == null) return;

            // 1. 头部颜色条
            var headerBit = door.Bits
                .Where(b => b.BitValue && b.HeaderPriority > 0)
                .OrderByDescending(b => b.HeaderPriority)
                .FirstOrDefault();
            
            var oldHeader = door.Visual.HeaderBackground;
            door.Visual.HeaderBackground = headerBit?.HeaderColor ?? Brushes.Gray;
            door.Visual.HeaderText = door.DoorName;

            // 2. 中间图形
            var imageBit = door.Bits
                .Where(b => b.BitValue && !string.IsNullOrEmpty(b.GraphicName))
                .OrderByDescending(b => b.ImagePriority)
                .ThenByDescending(b => b.BitId)
                .FirstOrDefault();

            if (imageBit != null)
            {
                // Debug.WriteLine($"[Adjudicate] Door: {door.DoorName}, GraphicName: '{imageBit.GraphicName}'");

                if (GlobalData.GraphicDictionary != null &&
                    GlobalData.GraphicDictionary.TryGetValue(imageBit.GraphicName.Trim(), out var templates))
                {
                    // LogHelper.Debug($"[DataManager] Drawing icons for {door.DoorName}: {imageBit.GraphicName}");
                    var newIcons = new List<IconItem>();
                    foreach (var t in templates)
                    {
                        var cloned = t.Clone();
                        
                        // 逻辑修正：不再强制覆盖颜色
                        // 因为"故障"等图标通常不仅是单色图形，而是包含背景、边框、主体等多色组合的复杂矢量图
                        // 如果强制染成红色，会导致整个图标变成一个红色方块
                        // 既然 GraphicDictionary 里已经定义好了颜色（如灰底红三角），直接使用原色即可
                        
                        /* 
                        if (imageBit.GraphicColor != null && imageBit.GraphicColor != Brushes.Black)
                        {
                             // ... (Color Override Logic Disabled) ...
                        }
                        */
                        
                        if (cloned.Data != null && cloned.Data.CanFreeze) cloned.Data.Freeze();
                        if (cloned.Fill != null && cloned.Fill.CanFreeze) cloned.Fill.Freeze();
                        if (cloned.Stroke != null && cloned.Stroke.CanFreeze) cloned.Stroke.Freeze();
                        
                        newIcons.Add(cloned);
                    }
                    door.Visual.Icons = newIcons;
                }
                else
                {
                    if (!string.IsNullOrEmpty(imageBit.GraphicName))
                    {
                        // 详细调试：打印名称长度和不可见字符
                        var name = imageBit.GraphicName;
                        LogHelper.Debug($"[Visual-Err] Graphic NOT found. Name:'{name}' Len:{name.Length} Door:{door.DoorName}");
                        
                        // 仅在首次失败时打印一次字典键，避免刷屏
                        if (!_debugKeysPrinted && GlobalData.GraphicDictionary != null)
                        {
                            var keys = string.Join(",", GlobalData.GraphicDictionary.Keys);
                            LogHelper.Debug($"[Visual-Err] Available Keys: [{keys}]");
                            _debugKeysPrinted = true;
                        }
                    }
                    if (door.Visual.Icons?.Count > 0) door.Visual.Icons = new List<IconItem>();
                }
            }
            else
            {
               // Debug.WriteLine($"[Adjudicate] Door: {door.DoorName}, No active image bit.");
               if (door.Visual.Icons?.Count > 0) door.Visual.Icons = new List<IconItem>();
            }
            
            // 默认兜底逻辑：如果没有任何状态激活，且没有图标，则显示"关门"状态
            if ((door.Visual.Icons == null || door.Visual.Icons.Count == 0) && GlobalData.GraphicDictionary != null)
            {
                 if (GlobalData.GraphicDictionary.TryGetValue("关门", out var defaultTemplates))
                 {
                    var defaultIcons = new List<IconItem>();
                    foreach (var t in defaultTemplates)
                    {
                        var cloned = t.Clone();
                        if (cloned.Data != null && cloned.Data.CanFreeze) cloned.Data.Freeze();
                        if (cloned.Fill != null && cloned.Fill.CanFreeze) cloned.Fill.Freeze();
                        defaultIcons.Add(cloned);
                    }
                    door.Visual.Icons = defaultIcons;
                 }
                 else 
                 {
                     // Debug.WriteLine($"[Visual-Err] Default '关门' graphic not found.");
                 }
            }

            // 3. 底部锁闭条
            var bottomBit = door.Bits
                .Where(b => b.BitValue && b.BottomPriority > 0)
                .OrderByDescending(b => b.BottomPriority)
                .FirstOrDefault();
            door.Visual.BottomBackground = bottomBit?.BottomColor ?? Brushes.Transparent;
            
            // 调试日志（仅在有变化或有激活点位时输出，避免刷屏）
            if (headerBit != null || imageBit != null || bottomBit != null)
            {
                // LogHelper.Debug($"[Visual] Door:{door.DoorName} Header:{headerBit?.Description} Img:{imageBit?.GraphicName} Bottom:{bottomBit?.Description}");
            }
        }

        #endregion
    }
}
