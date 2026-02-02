using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.RunModels;
using MySql.Data.MySqlClient;
using DoorMonitorSystem.Assets.Services;
using Communicationlib.config;
using ConfigEntity = Communicationlib.config.ConfigEntity;
using Base;
using DoorMonitorSystem.Base;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 点位配置视图模型 - 导入导出模块
    /// 负责与 Excel 进行交互，实现配置数据的批量导出备份和导入迁移。
    /// </summary>
    public partial class DevvarlistViewModel
    {
        #region 导入导出逻辑 (Excel)

        /// <summary>
        /// 将当前设备下的所有点位配置导出为 Excel 文件。
        /// 采用了 MiniExcel 以支持高效的大规模数据写入。
        /// </summary>
        private void ExportPoints()
        {
            if (SelectedDevice == null) return;
            if (Points.Count == 0)
            {
                MessageBox.Show("当前设备没有点位可导出。");
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                FileName = $"点位表_{SelectedDevice.Name}_{DateTime.Now:yyyyMMdd}.xlsx"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    // 构造导出的匿名对象集合，映射数据库字段到中文表头。
                    // 由于目前 Points 是 DevicePointRow 展示模型，需访问其内部的 Entity 实体。
                    var exportData = Points.Select(p => new
                    {
                        序号 = p.RowIndex,
                        物理地址 = p.Entity.Address,
                        位索引 = p.Entity.BitIndex,
                        数据类型 = p.Entity.DataType,
                        备注说明 = p.Entity.Description,
                        逻辑绑定键 = p.Entity.UiBinding,
                        绑定角色 = p.Entity.BindingRole,
                        目标类型 = (int)p.Entity.TargetType,
                        目标对象ID = p.Entity.TargetKeyId,
                        目标位配置ID = p.Entity.TargetBitConfigKeyId,
                        开启同步 = p.Entity.IsSyncEnabled ? 1 : 0,
                        同步目标设备ID = p.Entity.SyncTargetDeviceId,
                        同步写入地址 = p.Entity.SyncTargetAddress,
                        同步目标位索引 = p.Entity.SyncTargetBitIndex,
                        同步模式 = p.Entity.SyncMode,
                        开启日志 = p.Entity.IsLogEnabled ? 1 : 0,
                        日志类型ID = p.Entity.LogTypeId,
                        日志内容模板 = p.Entity.LogMessage,
                        日志分类 = p.Entity.Category,
                        高限报警 = p.Entity.HighLimit,
                        低限报警 = p.Entity.LowLimit,
                        状态0描述 = p.Entity.State0Desc,
                        状态1描述 = p.Entity.State1Desc,
                        报警目标值 = p.Entity.AlarmTargetValue
                    });

                    MiniExcelLibs.MiniExcel.SaveAs(sfd.FileName, exportData);
                    MessageBox.Show("导出成功！");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 从 Excel 文件批量导入点位配置到当前设备。
        /// 包含事务处理以确保数据完整性，并支持根据地址自动判断新增或更新。
        /// </summary>
        private async void ImportPoints()
        {
            if (SelectedDevice == null)
            {
                MessageBox.Show("请先选择一个目标设备。");
                return;
            }

            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel 文件 (*.xlsx)|*.xlsx"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var rows = MiniExcelLibs.MiniExcel.Query(ofd.FileName).ToList();
                    if (rows.Count <= 1)
                    {
                        MessageBox.Show("Excel 文件内容为空或格式不正确。");
                        return;
                    }

                    var dataRows = rows.Skip(1).Cast<IDictionary<string, object>>().ToList();

                    if (MessageBox.Show($"准备从 Excel 导入 {dataRows.Count} 个点位到设备 [{SelectedDevice.Name}]。\n\n是否继续？", "导入确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;

                    int successCount = 0;
                    int errorCount = 0;

                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();

                    // 加载缓存以便判断是 Update 还是 Insert。
                    var existingPoints = db.FindAll<DevicePointConfigEntity>("SourceDeviceId = @sid", new MySqlParameter("@sid", SelectedDevice.ID))
                                           .ToDictionary(p => $"{p.Address}_{p.BitIndex}");

                    db.BeginTransaction();
                    try 
                    {
                        foreach (var row in dataRows)
                        {
                            try
                            {
                                // 安全获取整数的可空值转换函数。
                                Func<string, int, int?> GetSafeInt = (k, idx) => 
                                {
                                    object val = row.ContainsKey(k) ? row[k] : row.Values.ElementAtOrDefault(idx);
                                    if (val == null || val is DBNull || string.IsNullOrWhiteSpace(val.ToString())) return null;
                                    try { return Convert.ToInt32(val); } catch { return null; } 
                                };

                                var entity = new DevicePointConfigEntity
                                {
                                    SourceDeviceId = SelectedDevice.ID,
                                    Address = row.ContainsKey("物理地址") ? row["物理地址"]?.ToString() : row.Values.ElementAtOrDefault(1)?.ToString(),
                                    BitIndex = Convert.ToInt32(row.ContainsKey("位索引") ? (row["位索引"] ?? 0) : (row.Values.ElementAtOrDefault(2) ?? 0)),
                                    DataType = row.ContainsKey("数据类型") ? row["数据类型"]?.ToString() : row.Values.ElementAtOrDefault(3)?.ToString() ?? "Word",
                                    Description = row.ContainsKey("备注说明") ? row["备注说明"]?.ToString() : row.Values.ElementAtOrDefault(4)?.ToString(),
                                    UiBinding = row.ContainsKey("逻辑绑定键") ? row["逻辑绑定键"]?.ToString() : row.Values.ElementAtOrDefault(5)?.ToString(),
                                    BindingRole = row.ContainsKey("绑定角色") ? row["绑定角色"]?.ToString() : row.Values.ElementAtOrDefault(6)?.ToString(),
                                    TargetType = (TargetType)Convert.ToInt32(row.ContainsKey("目标类型") ? (row["目标类型"] ?? 0) : (row.Values.ElementAtOrDefault(7) ?? 0)),
                                    TargetKeyId = row.ContainsKey("目标对象ID") ? row["目标对象ID"]?.ToString() : row.Values.ElementAtOrDefault(8)?.ToString(),
                                    TargetBitConfigKeyId = row.ContainsKey("目标位配置ID") ? row["目标位配置ID"]?.ToString() : row.Values.ElementAtOrDefault(9)?.ToString(),
                                    IsSyncEnabled = Convert.ToInt32(row.ContainsKey("开启同步") ? (row["开启同步"] ?? 0) : (row.Values.ElementAtOrDefault(10) ?? 0)) == 1,
                                    SyncTargetDeviceId = GetSafeInt("同步目标设备ID", 11),
                                    SyncTargetAddress = (ushort?)GetSafeInt("同步写入地址", 12),
                                    SyncTargetBitIndex = GetSafeInt("同步目标位索引", 13),
                                    SyncMode = Convert.ToInt32(row.ContainsKey("同步模式") ? (row["同步模式"] ?? 0) : (row.Values.ElementAtOrDefault(14) ?? 0)),
                                    IsLogEnabled = Convert.ToInt32(row.ContainsKey("开启日志") ? (row["开启日志"] ?? 0) : (row.Values.ElementAtOrDefault(15) ?? 0)) == 1,
                                    LogTypeId = Convert.ToInt32(row.ContainsKey("日志类型ID") ? (row["日志类型ID"] ?? 1) : (row.Values.ElementAtOrDefault(16) ?? 1)),
                                    LogMessage = row.ContainsKey("日志内容模板") ? row["日志内容模板"]?.ToString() : row.Values.ElementAtOrDefault(17)?.ToString(),
                                    Category = row.ContainsKey("日志分类") ? row["日志分类"]?.ToString() : row.Values.ElementAtOrDefault(18)?.ToString(),
                                    HighLimit = row.ContainsKey("高限报警") && row["高限报警"] != null ? (double?)Convert.ToDouble(row["高限报警"]) : null,
                                    LowLimit = row.ContainsKey("低限报警") && row["低限报警"] != null ? (double?)Convert.ToDouble(row["低限报警"]) : null,
                                    State0Desc = row.ContainsKey("状态0描述") ? row["状态0描述"]?.ToString() : row.Values.ElementAtOrDefault(21)?.ToString(),
                                    State1Desc = row.ContainsKey("状态1描述") ? row["状态1描述"]?.ToString() : row.Values.ElementAtOrDefault(22)?.ToString(),
                                    AlarmTargetValue = row.ContainsKey("报警目标值") && row["报警目标值"] != null ? (int?)Convert.ToInt32(row["报警目标值"]) : null
                                };

                                if (string.IsNullOrEmpty(entity.Address)) continue;

                                string key = $"{entity.Address}_{entity.BitIndex}";
                                if (existingPoints.TryGetValue(key, out var existing))
                                {
                                    entity.Id = existing.Id;
                                    db.Update(entity);
                                }
                                else
                                {
                                    db.Insert(entity);
                                }
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                LogHelper.Error($"导入行失败: {ex.Message}");
                            }
                        }
                        db.CommitTransaction();
                    }
                    catch (Exception ex)
                    {
                        db.RollbackTransaction();
                        throw new Exception($"导入过程发生异常，已回滚事务: {ex.Message}", ex);
                    }

                    MessageBox.Show($"导入完成！\n成功: {successCount} 条\n异常: {errorCount} 条");
                    LoadPoints(); 
                    DeviceCommunicationService.Instance?.ReloadConfigs();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
