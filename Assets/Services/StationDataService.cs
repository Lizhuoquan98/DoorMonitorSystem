using ControlLibrary.Models;
using Dapper;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Assets.Helper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace DoorMonitorSystem.Assets.Services
{
    /// <summary>
    /// 站台数据加载服务
    /// 负责从数据库加载配置数据并转换为运行时模型
    /// </summary>
    public class StationDataService
    {
        private readonly string _connectionString;
        private Dictionary<int, BitCategoryModel> _categoryCache = new();
        private Dictionary<int, Brush> _brushCache = new();

        public StationDataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// 从数据库加载所有分类数据并缓存
        /// </summary>
        private void LoadCategories(MySqlConnection conn)
        {
            _categoryCache.Clear();

            var categoryEntities = conn.Query<BitCategoryEntity>(
                "SELECT * FROM BitCategory ORDER BY SortOrder"
            ).ToList();

            foreach (var entity in categoryEntities)
            {
                var category = new BitCategoryModel
                {
                    CategoryId = entity.Id,
                    Code = entity.Code,
                    Name = entity.Name,
                    Icon = entity.Icon,
                    BackgroundColor = entity.BackgroundColor,
                    ForegroundColor = entity.ForegroundColor,
                    SortOrder = entity.SortOrder,
                    LayoutRows = entity.LayoutRows,
                    LayoutColumns = entity.LayoutColumns
                };

                _categoryCache[entity.Id] = category;
            }

            // Debug.WriteLine($"成功加载 {_categoryCache.Count} 个分类");
        }

        /// <summary>
        /// 加载所有颜色定义并缓存为 Brush
        /// </summary>
        private void LoadColors(MySqlConnection conn)
        {
            _brushCache.Clear();
            var colorEntities = conn.Query<BitColorEntity>("SELECT * FROM BitColor").ToList();
            foreach (var entity in colorEntities)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(entity.ColorValue);
                    var brush = new SolidColorBrush(color);
                    if (brush.CanFreeze) brush.Freeze();
                    _brushCache[entity.Id] = brush;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ColorLoad] Failed to parse color: {entity.ColorValue}, {ex.Message}");
                }
            }
        }

        private Brush GetBrush(int? id, Brush defaultBrush)
        {
            if (id.HasValue && _brushCache.TryGetValue(id.Value, out var brush))
                return brush;
            return defaultBrush;
        }

        /// <summary>
        /// 从数据库加载所有站台数据
        /// </summary>
        public List<StationMainGroup> LoadAllStations()
        {
            var stations = new List<StationMainGroup>();

            try
            {
                using var conn = new MySqlConnection(_connectionString);
                conn.Open();

                // 0. 首先加载基础字典数据到缓存
                LoadCategories(conn);
                LoadColors(conn);

                // 1. 加载所有站台
                var stationEntities = conn.Query<StationEntity>(
                    "SELECT * FROM Station ORDER BY SortOrder"
                ).ToList();

                foreach (var stationEntity in stationEntities)
                {
                    var station = ConvertToStationMainGroup(stationEntity, conn);
                    stations.Add(station);
                }

                // Debug.WriteLine($"成功加载 {stations.Count} 个站台");
            }
            catch (Exception ex)
            {
                LogHelper.Error("加载所有站台数据失败", ex);
            }

            return stations;
        }

        /// <summary>
        /// 转换站台实体为运行时模型
        /// </summary>
        private StationMainGroup ConvertToStationMainGroup(StationEntity entity, MySqlConnection conn)
        {
            var station = new StationMainGroup
            {
                StationId = entity.Id,
                KeyId = entity.KeyId,
                StationName = entity.StationName,
                StationCode = entity.StationCode,
                StationType = (StationType)entity.StationType,
                SortOrder = entity.SortOrder
            };

            // 加载门组 (传入 Id 用于运行时模型，KeyId 用于 SQL 查询)
            var doorGroups = LoadDoorGroups(entity.Id, entity.KeyId, conn);
            foreach (var doorGroup in doorGroups)
            {
                station.DoorGroups.Add(doorGroup);
            }

            // 加载面板组 (传入 Id 用于运行时模型，KeyId 用于 SQL 查询)
            var panelGroups = LoadPanelGroups(entity.Id, entity.KeyId, conn);
            foreach (var panelGroup in panelGroups)
            {
                station.PanelGroups.Add(panelGroup);
            }

            return station;
        }

        /// <summary>
        /// 加载站台的所有门组
        /// </summary>
        private List<DoorGroup> LoadDoorGroups(int stationId, string stationKeyId, MySqlConnection conn)
        {
            var doorGroups = new List<DoorGroup>();

            var doorGroupEntities = conn.Query<DoorGroupEntity>(
                "SELECT * FROM DoorGroup WHERE StationKeyId = @StationKeyId ORDER BY SortOrder",
                new { StationKeyId = stationKeyId }
            ).ToList();

            foreach (var entity in doorGroupEntities)
            {
                var doorGroup = new DoorGroup
                {
                    DoorGroupId = entity.Id,
                    KeyId = entity.KeyId,
                    StationId = stationId,
                    SortOrder = entity.SortOrder
                };

                // 加载门组中的所有门
                var doors = LoadDoors(entity.Id, entity.KeyId, conn);
                
                // 如果配置为逆序，则反转门列表
                if (entity.IsReverseOrder)
                {
                    doors.Reverse();
                }

                foreach (var door in doors)
                {
                    doorGroup.Doors.Add(door);
                }

                doorGroups.Add(doorGroup);
            }

            return doorGroups;
        }

        /// <summary>
        /// 加载门组的所有门
        /// </summary>
        private List<DoorModel> LoadDoors(int doorGroupId, string parentKeyId, MySqlConnection conn)
        {
            var doors = new List<DoorModel>();

            var doorEntities = conn.Query<DoorEntity>(
                "SELECT * FROM Door WHERE ParentKeyId = @ParentKeyId ORDER BY SortOrder",
                new { ParentKeyId = parentKeyId }
            ).ToList();

            foreach (var entity in doorEntities)
            {
                // 获取门类型信息 (关键: 使用 DoorTypeKeyId)
                var doorTypeEntity = conn.QueryFirstOrDefault<DoorTypeEntity>(
                    "SELECT * FROM DoorType WHERE KeyId = @DoorTypeKeyId",
                    new { DoorTypeKeyId = entity.DoorTypeKeyId }
                );

                var door = new DoorModel
                {
                    DoorId = entity.Id,
                    KeyId = entity.KeyId,
                    DoorGroupId = doorGroupId,
                    DoorName = entity.DoorName,
                    DoorType = DoorType.SlidingDoor, // 默认值，实际从 Code 映射
                    DoorTypeCode = doorTypeEntity?.Code ?? "",
                    SortOrder = entity.SortOrder,
                    Visual = new DoorVisualResult
                    {
                        HeaderText = entity.DoorName,
                        HeaderBackground = Brushes.Gray,
                        BottomBackground = Brushes.Green
                    }
                };

                // 加载门的点位配置（从门类型的点位模板复制）
                if (doorTypeEntity != null)
                {
                    var bitConfigs = LoadDoorBitConfigs(doorTypeEntity.Id, doorTypeEntity.KeyId, conn);
                    foreach (var bitConfig in bitConfigs)
                    {
                        door.Bits.Add(bitConfig);
                    }
                }

                doors.Add(door);
            }

            return doors;
        }

        /// <summary>
        /// 加载门类型的点位配置模板
        /// </summary>
        private List<DoorBitConfig> LoadDoorBitConfigs(int doorTypeId, string doorTypeKeyId, MySqlConnection conn)
        {
            var bitConfigs = new List<DoorBitConfig>();

            var bitEntities = conn.Query<DoorBitConfigEntity>(
                "SELECT * FROM DoorBitConfig WHERE DoorTypeKeyId = @DoorTypeKeyId ORDER BY SortOrder",
                new { DoorTypeKeyId = doorTypeKeyId }
            ).ToList();

            foreach (var entity in bitEntities)
            {
                var bitConfig = new DoorBitConfig
                {
                    BitId = entity.Id,
                    KeyId = entity.KeyId,
                    Description = entity.Description,
                    BitValue = false,
                    BindingDoorType = DoorType.SlidingDoor, // 这里原先强转 enum，GUID 系统下需要另行处理或固定
                    CategoryId = entity.CategoryId,
                    SortOrder = entity.SortOrder,
                    HeaderPriority = entity.HeaderPriority,
                    ImagePriority = entity.ImagePriority,
                    BottomPriority = entity.BottomPriority,
                    // 从数据库加载颜色配置
                    HighBrush = GetBrush(entity.HighColorId, Brushes.LimeGreen),
                    LowBrush = GetBrush(entity.LowColorId, Brushes.DarkGray),
                    
                    HeaderColor = GetBrush(entity.HeaderColorId ?? entity.HighColorId, Brushes.Gray),
                    GraphicName = entity.GraphicName ?? "",
                    GraphicColor = GetBrush(entity.HighColorId, Brushes.Black),
                    BottomColor = GetBrush(entity.BottomColorId ?? entity.HighColorId, Brushes.Green)
                };

                // 关联分类对象
                if (entity.CategoryId.HasValue && _categoryCache.ContainsKey(entity.CategoryId.Value))
                {
                    bitConfig.Category = _categoryCache[entity.CategoryId.Value];
                }

                bitConfigs.Add(bitConfig);
            }

            return bitConfigs;
        }

        /// <summary>
        /// 加载站台的所有面板组
        /// </summary>
        private List<PanelGroup> LoadPanelGroups(int stationId, string stationKeyId, MySqlConnection conn)
        {
            var panelGroups = new List<PanelGroup>();

            var panelGroupEntities = conn.Query<PanelGroupEntity>(
                "SELECT * FROM PanelGroup WHERE StationKeyId = @StationKeyId ORDER BY SortOrder",
                new { StationKeyId = stationKeyId }
            ).ToList();

            foreach (var entity in panelGroupEntities)
            {
                var panelGroup = new PanelGroup
                {
                    PanelGroupId = entity.Id,
                    KeyId = entity.KeyId,
                    StationId = stationId,
                    SortOrder = entity.SortOrder
                };

                // 加载面板组中的所有面板
                var panels = LoadPanels(entity.Id, entity.KeyId, conn);
                foreach (var panel in panels)
                {
                    panelGroup.Panels.Add(panel);
                }

                panelGroups.Add(panelGroup);
            }

            return panelGroups;
        }

        /// <summary>
        /// 加载面板组的所有面板
        /// </summary>
        private List<PanelModel> LoadPanels(int panelGroupId, string parentKeyId, MySqlConnection conn)
        {
            var panels = new List<PanelModel>();

            var panelEntities = conn.Query<PanelEntity>(
                "SELECT * FROM Panel WHERE ParentKeyId = @ParentKeyId ORDER BY SortOrder",
                new { ParentKeyId = parentKeyId }
            ).ToList();

            foreach (var entity in panelEntities)
            {
                var panel = new PanelModel
                {
                    PanelId = entity.Id,
                    KeyId = entity.KeyId,
                    PanelGroupId = panelGroupId,
                    PanelName = entity.PanelName,
                    TitlePosition = (PanelTitlePosition)entity.TitlePosition,
                    LayoutRows = entity.LayoutRows,
                    LayoutColumns = entity.LayoutColumns,
                    SortOrder = entity.SortOrder
                };

                // 加载面板的点位配置 (传入 PanelKeyId)
                var bitConfigs = LoadPanelBitConfigs(entity.Id, entity.KeyId, conn);
                foreach (var bitConfig in bitConfigs)
                {
                    panel.BitList.Add(bitConfig);
                }

                panels.Add(panel);
            }

            return panels;
        }

        /// <summary>
        /// 加载面板的点位配置（从面板实例对应的配置表获取）
        /// </summary>
        private List<PanelBitConfig> LoadPanelBitConfigs(int panelId, string panelKeyId, MySqlConnection conn)
        {
            var bitConfigs = new List<PanelBitConfig>();

            if (string.IsNullOrEmpty(panelKeyId)) return bitConfigs;

            var bitEntities = conn.Query<PanelBitConfigEntity>(
                "SELECT * FROM PanelBitConfig WHERE PanelKeyId = @PanelKeyId ORDER BY SortOrder",
                new { PanelKeyId = panelKeyId }
            ).ToList();

            foreach (var entity in bitEntities)
            {
                var bitConfig = new PanelBitConfig
                {
                    BitId = entity.Id,
                    KeyId = entity.KeyId,
                    PanelId = panelId,
                    Description = entity.Description,
                    BitValue = false,
                    SortOrder = entity.SortOrder,
                    HighBrush = GetBrush(entity.HighColorId, Brushes.LimeGreen),
                    LowBrush = GetBrush(entity.LowColorId, Brushes.DarkGray)
                };

                bitConfigs.Add(bitConfig);
            }

            return bitConfigs;
        }
    }


}
