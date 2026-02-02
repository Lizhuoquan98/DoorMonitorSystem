using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Assets.Services;
using DoorMonitorSystem.Models.Ui;
using Base;
using DoorMonitorSystem.Base;
using Communicationlib.config;
using ConfigEntity = Communicationlib.config.ConfigEntity;
using MySql.Data.MySqlClient;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 点位配置视图模型 - 向导与批量工具
    /// </summary>
    public partial class DevvarlistViewModel
    {
        #region 批量生成向导 (Batch Wizard Logic)

        /// <summary>
        /// 打开批量生成对话框并初始化数据。
        /// </summary>
        private void OpenBatchPopup(object obj)
        {
            IsBatchPopupVisible = true;
            
            // 加载站台列表
            BatchStations.Clear();
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                var stations = db.FindAll<StationEntity>();
                foreach (var s in stations) BatchStations.Add(s);
            }
            catch (Exception ex) { LogHelper.Error("加载站台列表失败", ex); }

            if (BatchStations.Count > 0 && SelectedBatchStation == null) SelectedBatchStation = BatchStations[0];
            if (string.IsNullOrEmpty(SelectedBatchTargetType)) SelectedBatchTargetType = BatchTargetTypes[0];

            // 核心修复：弹窗打开时，即使是默认选中的站台，也要强制触发一次组加载。
            LoadBatchGroups();
        }

        /// <summary>
        /// 根据选中的站台和类型加载具体的组列表 (门组/面板组)。
        /// 此方法会查询数据库中关联到当前选中站台的所有门组或面板组，并填充到 BatchGroups 集合中。
        /// </summary>
        private void LoadBatchGroups()
        {
            if (SelectedBatchStation == null) return;
            
            BatchGroups.Clear();
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                if (!db.DatabaseExists()) return;
                db.Connect();

                if (SelectedBatchTargetType == "门点位")
                {
                    var groups = db.FindAll<DoorGroupEntity>().Where(g => g.StationKeyId == SelectedBatchStation.KeyId).ToList();
                    foreach (var g in groups)
                        BatchGroups.Add(new DoorMonitorSystem.Models.Ui.BatchGroupItem { Name = g.GroupName, TargetType = TargetType.Door, IsSelected = true, KeyId = g.KeyId });
                }
                else if (SelectedBatchTargetType == "面板点位")
                {
                    var groups = db.FindAll<PanelGroupEntity>().Where(g => g.StationKeyId == SelectedBatchStation.KeyId).ToList();
                    foreach (var g in groups)
                        BatchGroups.Add(new DoorMonitorSystem.Models.Ui.BatchGroupItem { Name = g.GroupName, TargetType = TargetType.Panel, IsSelected = true, KeyId = g.KeyId });
                }
                else if (SelectedBatchTargetType == "站台参数")
                {
                    BatchGroups.Add(new DoorMonitorSystem.Models.Ui.BatchGroupItem { Name = "系统参数组", TargetType = TargetType.Station, IsSelected = true });
                }

                if (BatchGroups.Count > 0) SelectedBatchGroup = BatchGroups[0];
            }
            catch (Exception ex) { LogHelper.Error("加载批处理组失败", ex); }
        }

        /// <summary>
        /// 执行批量生成逻辑。
        /// 核心流程：
        /// 1. 验证用户输入（设备、站台、目标组）。
        /// 2. 根据模板（DoorBitConfig/PanelBitConfig）和物理对象（Door/Panel）生成预期点位列表。
        /// 3. 执行数据库事务更新：
        ///    - 优先匹配业务 ID (TargetKey + BitKey)。
        ///    - 其次匹配物理地址 (Address + BitIndex)。
        ///    - 存在则更新（保留原有高级配置如同步、转发），不存在则插入。
        /// </summary>
        private void GenerateBatchPoints(object obj)
        {
            if (SelectedDevice == null) { MessageBox.Show("请先选择目标设备"); return; }
            if (SelectedBatchStation == null) { MessageBox.Show("请先选择站台"); return; }
            
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();

                List<DevicePointConfigEntity> resultPoints = new List<DevicePointConfigEntity>();
                
                // 解析起始地址 (简单假设为整数)
                int startAddr = 0;
                int.TryParse(BatchStartAddress, out startAddr);

                if (SelectedBatchTargetType == "门点位")
                {
                    if (SelectedBatchGroup == null) { MessageBox.Show("请选择目标门组"); return; }
                    
                    // 仅获取属于选定门组的门
                    var doors = db.FindAll<DoorEntity>()
                                  .Where(d => d.ParentKeyId == SelectedBatchGroup.KeyId)
                                  .OrderBy(d => d.SortOrder)
                                  .ToList();
                                  
                    var templates = db.FindAll<DoorBitConfigEntity>().ToList();
                    resultPoints = DbPointGenerator.GenerateDoorPoints(doors, templates, SelectedDevice.ID, startAddr, BatchStride, SelectedDevice.Protocol);
                }
                else if (SelectedBatchTargetType == "面板点位")
                {
                    if (SelectedBatchGroup == null) { MessageBox.Show("请选择目标面板组"); return; }
                    
                    // 仅获取属于选定面板组的面板
                    var panels = db.FindAll<PanelEntity>()
                                   .Where(p => p.ParentKeyId == SelectedBatchGroup.KeyId)
                                   .OrderBy(p => p.SortOrder)
                                   .ToList();
                                   
                    var templates = db.FindAll<PanelBitConfigEntity>().ToList();
                    resultPoints = DbPointGenerator.GeneratePanelPoints(panels, templates, SelectedDevice.ID, startAddr, BatchStride, SelectedDevice.Protocol);
                }
                else if (SelectedBatchTargetType == "站台参数")
                {
                    // 站台级参数通常关联 StationEntity 本身
                    var templates = db.FindAll<ParameterDefineEntity>().ToList();
                    
                    foreach(var temp in templates)
                    {
                         resultPoints.Add(new DevicePointConfigEntity {
                             SourceDeviceId = SelectedDevice.ID,
                             TargetType = TargetType.Station,
                             TargetKeyId = SelectedBatchStation.KeyId,
                             TargetBitConfigKeyId = temp.KeyId,
                             Description = $"{SelectedBatchStation.StationName}-{temp.Label}", 
                             Address = (startAddr + temp.ByteOffset).ToString(),
                             BitIndex = temp.BitIndex,
                             DataType = temp.DataType ?? "Int16", 
                             State0Desc = "取消",
                             State1Desc = "触发",
                             IsLogEnabled = true
                         });
                    }
                }

                if (resultPoints.Count == 0)
                {
                    MessageBox.Show("未生成任何点位，请检查是否已配置模板且站台下有相应的对象。");
                    return;
                }

                if (MessageBox.Show($"向导已生成 {resultPoints.Count} 个新点位，是否保存到设备 [{SelectedDevice.Name}]？\n(注意：重复物理地址的点位将被更新)", "批量导入确认", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    db.BeginTransaction();
                    try
                    {
                        // 1. 获取当前设备的所有点位，建立多索引映射
                        var allPoints = db.FindAll<DevicePointConfigEntity>("SourceDeviceId = @sid", new MySqlParameter("@sid", SelectedDevice.ID));
                        
                        // 业务主键字典 (业务ID+功能ID) - 优先级 1
                        var logicMap = allPoints.Where(p => !string.IsNullOrEmpty(p.TargetKeyId) && !string.IsNullOrEmpty(p.TargetBitConfigKeyId))
                                                .GroupBy(p => $"{p.TargetKeyId}_{p.TargetBitConfigKeyId}")
                                                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                        // 物理地址字典 (地址+位索引) - 优先级 2 (兜底匹配旧数据)
                        var addrMap = allPoints.GroupBy(p => $"{p.Address}_{p.BitIndex}")
                                               .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                        foreach (var p in resultPoints)
                        {
                            DevicePointConfigEntity? old = null;
                            string logicKey = $"{p.TargetKeyId}_{p.TargetBitConfigKeyId}";
                            string addrKey = $"{p.Address}_{p.BitIndex}";

                            // 优先通过业务身份匹配，匹配不到则通过物理地址匹配
                            if (logicMap.TryGetValue(logicKey, out old) || addrMap.TryGetValue(addrKey, out old))
                            {
                                p.Id = old.Id; // 复用自增 ID

                                // 保护转发配置
                                p.IsSyncEnabled = old.IsSyncEnabled;
                                p.SyncTargetDeviceId = old.SyncTargetDeviceId;
                                p.SyncTargetAddress = old.SyncTargetAddress;
                                p.SyncTargetBitIndex = old.SyncTargetBitIndex;
                                p.SyncMode = old.SyncMode;

                                // 智能填充描述（如果旧的描述是空或者是默认的 "/"，则应用新描述）
                                if (!string.IsNullOrWhiteSpace(old.State0Desc) && old.State0Desc != "/") p.State0Desc = old.State0Desc;
                                if (!string.IsNullOrWhiteSpace(old.State1Desc) && old.State1Desc != "/") p.State1Desc = old.State1Desc;

                                db.Update(p);
                            }
                            else
                            {
                                db.Insert(p);
                            }
                        }
                        db.CommitTransaction();
                        MessageBox.Show($"批量处理完成！共更新/生成 {resultPoints.Count} 个点位。");
                        IsBatchPopupVisible = false;
                        LoadPoints();
                        DeviceCommunicationService.Instance?.ReloadConfigs();
                    }
                    catch (Exception ex) 
                    { 
                        db.RollbackTransaction(); 
                        throw ex; 
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show($"生成失败: {ex.Message}"); }
        }

        #endregion
    }
}
