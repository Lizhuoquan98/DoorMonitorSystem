using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.RunModels;
using MySql.Data.MySqlClient;
using DoorMonitorSystem.Assets.Services;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using Communicationlib.config;
using ConfigEntity = Communicationlib.config.ConfigEntity;
using Base;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.Ui;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 点位配置视图模型 - 数据管理模块
    /// 包含基础的点表加载、单点位新增、修改、删除逻辑。
    /// 所有界面展现逻辑已迁移至 DevicePointRow 模型。
    /// </summary>
    public partial class DevvarlistViewModel
    {
        #region 数据加载与管理 (Points & Devices)

        /// <summary>
        /// 加载所有可用的控制器/设备列表。
        /// 优先从全局缓存读取以提高响应速度。
        /// </summary>
        private void LoadDevices()
        {
            Devices.Clear();
            try
            {
                if (GlobalData.ListDveices != null && GlobalData.ListDveices.Count > 0)
                {
                    foreach (var d in GlobalData.ListDveices) Devices.Add(d);
                }
                else
                {
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();
                    var list = db.FindAll<ConfigEntity>();
                    foreach (var d in list) Devices.Add(d);
                }
                // 默认选中第一个设备
                if (Devices.Count > 0 && SelectedDevice == null) SelectedDevice = Devices[0];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载设备列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载当前选中设备的点位配置。
        /// 实现了底层 Entity 到 UI 展示模型 DevicePointRow 的投影，并动态构建业务全路径。
        /// </summary>
        private void LoadPoints()
        {
            Points.Clear();
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return;
                db.Connect();

                var query = db.FindAll<DevicePointConfigEntity>();
                if (SelectedDevice != null)
                {
                    query = query.Where(x => x.SourceDeviceId == SelectedDevice.ID).ToList();
                }
                var list = query.OrderBy(x => x.Id).ToList(); // 默认按Id排序或保持原始顺序

                // 预加载相关业务实体的字典，避免在循环中频繁查询数据库，优化加载性能。
                var doorsByKey = db.FindAll<DoorEntity>().Where(d => !string.IsNullOrEmpty(d.KeyId)).ToDictionary(d => d.KeyId);
                var panelsByKey = db.FindAll<PanelEntity>().Where(p => !string.IsNullOrEmpty(p.KeyId)).ToDictionary(p => p.KeyId);
                var doorGroupsByKey = db.FindAll<DoorGroupEntity>().Where(g => !string.IsNullOrEmpty(g.KeyId)).ToDictionary(g => g.KeyId);
                var panelGroupsByKey = db.FindAll<PanelGroupEntity>().Where(g => !string.IsNullOrEmpty(g.KeyId)).ToDictionary(g => g.KeyId);
                var stationsByKey = db.FindAll<StationEntity>().Where(s => !string.IsNullOrEmpty(s.KeyId)).ToDictionary(s => s.KeyId);
                var doorConfigsByKey = db.FindAll<DoorBitConfigEntity>().Where(c => !string.IsNullOrEmpty(c.KeyId)).ToDictionary(c => c.KeyId);
                var panelConfigsByKey = db.FindAll<PanelBitConfigEntity>().Where(c => !string.IsNullOrEmpty(c.KeyId)).ToDictionary(c => c.KeyId);
                var stationParamsByBindingKey = db.FindAll<ParameterDefineEntity>().Where(p => !string.IsNullOrEmpty(p.BindingKey)).ToDictionary(p => p.BindingKey);

                int index = 1;
                foreach (var entity in list)
                {
                    // 创建展示层包装对象
                    var row = new DevicePointRow(entity) { RowIndex = index++ };
                    
                    // --- 动态构建业务对象全路径 (全息路径解析) ---
                    string fullPath = "";
                    if (entity.TargetType == TargetType.Door)
                    {
                        if (!string.IsNullOrEmpty(entity.TargetKeyId) && doorsByKey.TryGetValue(entity.TargetKeyId, out var door))
                        {
                            string groupName = "未知分组";
                            string stationName = "未知站台";
                            if (doorGroupsByKey.TryGetValue(door.ParentKeyId, out var group))
                            {
                                groupName = group.GroupName;
                                if (stationsByKey.TryGetValue(group.StationKeyId, out var station))
                                    stationName = station.StationName;
                            }
                            fullPath = $"{stationName} > {groupName} > {door.DoorName}";
                        }
                        if (!string.IsNullOrEmpty(entity.TargetBitConfigKeyId) && doorConfigsByKey.TryGetValue(entity.TargetBitConfigKeyId, out var cfg))
                        {
                            fullPath += $" > {cfg.Description}";
                        }
                    }
                    else if (entity.TargetType == TargetType.Panel)
                    {
                        if (!string.IsNullOrEmpty(entity.TargetKeyId) && panelsByKey.TryGetValue(entity.TargetKeyId, out var panel))
                        {
                            string groupName = "未知分组";
                            string stationName = "未知站台";
                            if (panelGroupsByKey.TryGetValue(panel.ParentKeyId, out var group))
                            {
                                groupName = group.GroupName;
                                if (stationsByKey.TryGetValue(group.StationKeyId, out var station))
                                    stationName = station.StationName;
                            }
                            fullPath = $"{stationName} > {groupName} > {panel.PanelName}";
                        }
                        if (!string.IsNullOrEmpty(entity.TargetBitConfigKeyId) && panelConfigsByKey.TryGetValue(entity.TargetBitConfigKeyId, out var pcfg))
                        {
                            fullPath += $" > {pcfg.Description}";
                        }
                    }
                    else if (entity.TargetType == TargetType.Station)
                    {
                        if (!string.IsNullOrEmpty(entity.TargetKeyId) && stationsByKey.TryGetValue(entity.TargetKeyId, out var station))
                        {
                             fullPath = $"{station.StationName} > 站台参数";
                             if (!string.IsNullOrEmpty(entity.UiBinding) && stationParamsByBindingKey.TryGetValue(entity.UiBinding, out var scfg))
                             {
                                 string roleName = entity.BindingRole switch {
                                     "Read" => "读取",
                                     "Write" => "写入",
                                     "Auth" => "鉴权",
                                     "AuthRow" => "行授权",
                                     _ => entity.BindingRole
                                 };
                                 string roleSuffix = string.IsNullOrEmpty(roleName) ? "" : $" ({roleName})";
                                 fullPath += $" > {scfg.Label}{roleSuffix}";
                             }
                        }
                    }
                    
                    row.BindingFullPath = string.IsNullOrEmpty(fullPath) ? "未绑定" : fullPath;

                    // 解析同步目标设备名称
                    if (entity.IsSyncEnabled && entity.SyncTargetDeviceId.HasValue)
                    {
                        var dev = Devices.FirstOrDefault(d => d.ID == entity.SyncTargetDeviceId.Value);
                        if (dev != null) row.SyncTargetDeviceName = dev.Name;
                    }

                    Points.Add(row);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载点表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 启动修改流程。
        /// 从选中的展示模型 (SelectedPoint) 中获取底层实体，并进行副本拷贝用于编辑。
        /// </summary>
        private void StartEdit(object obj)
        {
            if (SelectedPoint == null) return;
            var p = SelectedPoint.Entity; // 获取底层实体
            
            NewPoint = new DevicePointConfigEntity
            {
                Id = p.Id,
                SourceDeviceId = p.SourceDeviceId,
                Address = p.Address,
                BitIndex = p.BitIndex,
                DataType = p.DataType,
                FunctionCode = p.FunctionCode,
                Description = p.Description,
                Category = p.Category,
                UiBinding = p.UiBinding,
                BindingRole = p.BindingRole,
                TargetType = p.TargetType,
                TargetKeyId = p.TargetKeyId,
                TargetBitConfigKeyId = p.TargetBitConfigKeyId,
                IsSyncEnabled = p.IsSyncEnabled,
                SyncTargetDeviceId = p.SyncTargetDeviceId,
                SyncTargetAddress = p.SyncTargetAddress,
                SyncTargetBitIndex = p.SyncTargetBitIndex,
                SyncMode = p.SyncMode,
                IsLogEnabled = p.IsLogEnabled,
                LogTypeId = p.LogTypeId,
                LogMessage = p.LogMessage,
                LogTriggerState = p.LogTriggerState,
                LogDeadband = p.LogDeadband,
                HighLimit = p.HighLimit,
                LowLimit = p.LowLimit,
                State0Desc = p.State0Desc,
                State1Desc = p.State1Desc,
                AlarmTargetValue = p.AlarmTargetValue
            };
            IsEditing = true;
            IsEditorVisible = true;
        }

        /// <summary>
        /// 打开新增点位表单。
        /// </summary>
        private void OpenAddForm(object obj)
        {
            ResetForm();
            IsEditing = false;
            IsEditorVisible = true;
        }

        /// <summary>
        /// 取消编辑。
        /// </summary>
        private void CancelEdit(object obj)
        {
            IsEditing = false;
            IsEditorVisible = false;
            ResetForm();
        }

        /// <summary>
        /// 重置编辑表单。
        /// </summary>
        private void ResetForm()
        {
            if (SelectedDevice == null) return;
            IsEditing = false;
            NewPoint = new DevicePointConfigEntity 
            { 
                SourceDeviceId = SelectedDevice.ID,
                DataType = "Bool",
                TargetType = TargetType.None,
                IsSyncEnabled = false
            };
        }

        /// <summary>
        /// 保存点位配置（新增或更新）。
        /// 包含地址冲突校验和通讯引擎热重载。
        /// </summary>
        private void AddPoint(object obj)
        {
            if (SelectedDevice == null) { MessageBox.Show("请先选择设备"); return; }
            NewPoint.SourceDeviceId = SelectedDevice.ID;
            if (string.IsNullOrWhiteSpace(NewPoint.Address)) { MessageBox.Show("地址不能为空"); return; }
            if (NewPoint.IsSyncEnabled && (NewPoint.SyncTargetDeviceId == null || NewPoint.SyncTargetAddress == null))
            {
                MessageBox.Show("开启同步时，必须指定目标设备和同步地址");
                return;
            }

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                
                string dupCheckSql = "SELECT Description, Id FROM DevicePointConfig WHERE SourceDeviceId = @sid AND Address = @addr AND BitIndex = @bit AND Id <> @id";
                var dupResult = db.Query<DevicePointConfigEntity>(dupCheckSql, 
                    new MySqlParameter("@sid", NewPoint.SourceDeviceId),
                    new MySqlParameter("@addr", NewPoint.Address),
                    new MySqlParameter("@bit", NewPoint.BitIndex),
                    new MySqlParameter("@id", NewPoint.Id)).FirstOrDefault();

                if (dupResult != null) 
                { 
                    MessageBox.Show($"物理地址冲突！\n该设备下地址 [{NewPoint.Address}.{NewPoint.BitIndex}] 已被占用。\n冲突点位：[ID: {dupResult.Id}] {dupResult.Description}\n\n请修改地址后再试。", "保存验证", MessageBoxButton.OK, MessageBoxImage.Warning); 
                    return; 
                }

                if (IsEditing)
                {
                    db.Update(NewPoint);
                    LogHelper.Info($"[点表修改] 用户修改了设备 [{SelectedDevice.Name}] 下的点位: [ID: {NewPoint.Id}] {NewPoint.Description}, 地址: {NewPoint.Address}.{NewPoint.BitIndex}");
                }
                else
                {
                    db.Insert(NewPoint);
                    LogHelper.Info($"[点表新增] 用户在设备 [{SelectedDevice.Name}] 下新增了点位: {NewPoint.Description}, 地址: {NewPoint.Address}.{NewPoint.BitIndex}");
                }

                IsEditorVisible = false;
                LoadPoints();
                ResetForm();
                DeviceCommunicationService.Instance?.ReloadConfigs();
            }
            catch (Exception ex) 
            { 
                LogHelper.Error($"[点表保存] 操作失败: {ex.Message}", ex);
                MessageBox.Show($"保存失败: {ex.Message}"); 
            }
        }

        /// <summary>
        /// 物理删除选中点位。
        /// </summary>
        private void DeletePoint(object obj)
        {
            if (SelectedPoint == null) return;
            if (MessageBox.Show($"确定要删除点位 [{SelectedPoint.Entity.Description}] 吗？", "删除确认", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    var entity = SelectedPoint.Entity;
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();
                    db.Delete(entity); // 从展示层获取底层实体进行删除
                    LogHelper.Warn($"[点表删除] 用户删除了设备 [{SelectedDevice.Name}] 下的点位: [ID: {entity.Id}] {entity.Description}, 地址: {entity.Address}.{entity.BitIndex}");
                    LoadPoints();
                    DeviceCommunicationService.Instance?.ReloadConfigs();
                }
                catch (Exception ex) 
                { 
                    LogHelper.Error($"[点表删除] 删除失败: {ex.Message}", ex);
                    MessageBox.Show($"删除失败: {ex.Message}"); 
                }
            }
        }
        #endregion
    }
}
