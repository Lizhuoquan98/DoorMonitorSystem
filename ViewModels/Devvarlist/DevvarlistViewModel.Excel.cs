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
                        序号 = p.Entity.Id, // 导出数据库实际 ID，便于反向导入更新
                        序号显示 = p.RowIndex, // 仅供参考的行号
                        物理地址 = p.Entity.Address,
                        位索引 = p.Entity.BitIndex,
                        数据类型 = p.Entity.DataType,
                        备注说明 = p.Entity.Description,
                        逻辑绑定键 = p.Entity.UiBinding,
                        绑定角色 = p.Entity.BindingRole,
                        目标类型 = (int)p.Entity.TargetType,
                        目标对象ID = p.Entity.TargetKeyId,
                        目标位配置ID = p.Entity.TargetBitConfigKeyId,
                        功能码 = p.Entity.FunctionCode,
                        开启同步 = p.Entity.IsSyncEnabled ? 1 : 0,
                        同步目标设备ID = p.Entity.SyncTargetDeviceId,
                        同步写入地址 = p.Entity.SyncTargetAddress,
                        同步目标位索引 = p.Entity.SyncTargetBitIndex,
                        同步模式 = p.Entity.SyncMode,
                        开启日志 = p.Entity.IsLogEnabled ? 1 : 0,
                        日志类型ID = p.Entity.LogTypeId,
                        日志触发策略 = p.Entity.LogTriggerState,
                        日志死区 = p.Entity.LogDeadband,
                        日志内容模板 = p.Entity.LogMessage,
                        日志分类 = p.Entity.Category,
                        高限报警 = p.Entity.HighLimit,
                        低限报警 = p.Entity.LowLimit,
                        状态0描述 = p.Entity.State0Desc,
                        状态1描述 = p.Entity.State1Desc,
                        报警目标值 = p.Entity.AlarmTargetValue
                    });

                    MiniExcelLibs.MiniExcel.SaveAs(sfd.FileName, exportData);
                    LogHelper.Info($"[Excel导出] 用户导出了设备 [{SelectedDevice.Name}] 的点位表，共 {Points.Count} 条记录。文件路径: {sfd.FileName}");
                    MessageBox.Show("导出成功！");
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"[Excel导出] 导出失败: {ex.Message}", ex);
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
                    // 1. 获取现有内存点位，构建 ID 查找表和地址匹配池 (应对重复地址点位)
                    var dictById = new Dictionary<int, DevicePointConfigEntity>();
                    // 使用 List 作为池，按顺序匹配并“消费”，解决 0.0 地址重复导致的冲突。
                    var poolByAddr = new Dictionary<string, List<DevicePointConfigEntity>>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var p in Points)
                    {
                        var entity = p.Entity;
                        dictById[entity.Id] = entity;
                        
                        string addrKey = $"{entity.Address}_{entity.BitIndex}";
                        if (!poolByAddr.ContainsKey(addrKey)) poolByAddr[addrKey] = new List<DevicePointConfigEntity>();
                        poolByAddr[addrKey].Add(entity);
                    }

                    // 重新开启查询，启用 useHeaderRow 以通过列名精准读取，防止列偏移导致的数据缺失。
                    IEnumerable<dynamic> rows;
                    try
                    {
                        rows = MiniExcelLibs.MiniExcel.Query(ofd.FileName, useHeaderRow: true);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error($"[Excel导入] 无法读取文件: {ex.Message}");
                        MessageBox.Show($"读取文件失败: {ex.Message}");
                        return;
                    }

                    var dataRows = rows.Cast<IDictionary<string, object>>().ToList();
                    if (!dataRows.Any())
                    {
                        MessageBox.Show("未在 Excel 中发现有效数据行。");
                        return;
                    }

                    // 用于高性能解析的辅助方法
                    Func<IDictionary<string, object>, string, int, object> GetVal = (row, k, idx) => 
                    {
                        if (row.ContainsKey(k) && row[k] != null && !(row[k] is DBNull)) return row[k];
                        // 备选方案：如果列名没对上，尝试按位置拿（但危险，仅作为最后保底）
                        return row.Values.ElementAtOrDefault(idx);
                    };
                    
                    Func<object, int, int?> ParseInt = (val, def) => 
                    {
                        if (val == null || val is DBNull || string.IsNullOrWhiteSpace(val.ToString())) return def;
                        string s = val.ToString();
                        if (double.TryParse(s, out double d)) return (int)Math.Round(d);
                        return def;
                    };

                    Func<object, double?> ParseDouble = (val) => 
                    {
                        if (val == null || val is DBNull || string.IsNullOrWhiteSpace(val.ToString())) return null;
                        if (double.TryParse(val.ToString(), out double d)) return d;
                        return null;
                    };

                    Func<int?, int?, bool> IntEquals = (i1, i2) => (i1 ?? 0) == (i2 ?? 0);
                    Func<double?, double?, bool> DoubleEquals = (d1, d2) => Math.Abs((d1 ?? 0) - (d2 ?? 0)) < 0.0001;

                    Func<string, string, bool> StringEquals = (s1, s2) => 
                    {
                        return string.Equals(s1 ?? "", s2 ?? "", StringComparison.Ordinal);
                    };

                    if (MessageBox.Show($"准备从 Excel 导入 {dataRows.Count} 个点位到设备 [{SelectedDevice.Name}]。\n\n是否继续？", "导入确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;

                    int addCount = 0;
                    int updateCount = 0;
                    int skipCount = 0;
                    int errorCount = 0;

                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();

                    List<string> errorDetails = new List<string>();

                    db.BeginTransaction();
                    try 
                    {
                        foreach (var row in dataRows)
                        {
                            try
                            {
                                var addr = GetVal(row, "物理地址", 2)?.ToString();
                                if (string.IsNullOrEmpty(addr)) continue;

                                var bitIdx = ParseInt(GetVal(row, "位索引", 3), 0) ?? 0;
                                int? excelId = ParseInt(GetVal(row, "序号", 0), -1);
                                if (excelId == -1) excelId = null;

                                // 查找现有记录
                                DevicePointConfigEntity existing = null;
                                
                                // 策略 A：ID 优先匹配 (最准确)
                                if (excelId.HasValue && dictById.TryGetValue(excelId.Value, out var pById)) 
                                {
                                    existing = pById;
                                    // 匹配后从地址池中同步移除，防止后续地址模糊匹配误中
                                    string ak = $"{existing.Address}_{existing.BitIndex}";
                                    if (poolByAddr.TryGetValue(ak, out var list)) list.Remove(existing);
                                }
                                // 策略 B：地址池顺序匹配 (解决重复地址点位 A, B, C 的 1:1 映射)
                                else if (poolByAddr.TryGetValue($"{addr}_{bitIdx}", out var list) && list.Count > 0)
                                {
                                    existing = list[0];
                                    list.RemoveAt(0); // “消费”掉这个点位，下一行 0.0 将匹配池中的下一个点位
                                }

                                var entity = new DevicePointConfigEntity
                                {
                                    SourceDeviceId = SelectedDevice.ID,
                                    Address = addr,
                                    BitIndex = bitIdx,
                                    DataType = GetVal(row, "数据类型", 4)?.ToString() ?? "Word",
                                    Description = GetVal(row, "备注说明", 5)?.ToString(),
                                    UiBinding = GetVal(row, "逻辑绑定键", 6)?.ToString(),
                                    BindingRole = GetVal(row, "绑定角色", 7)?.ToString(),
                                    TargetType = (TargetType)(ParseInt(GetVal(row, "目标类型", 8), 0) ?? 0),
                                    TargetKeyId = GetVal(row, "目标对象ID", 9)?.ToString(),
                                    TargetBitConfigKeyId = GetVal(row, "目标位配置ID", 10)?.ToString(),
                                    FunctionCode = ParseInt(GetVal(row, "功能码", 11), 0) ?? 0,
                                    IsSyncEnabled = ParseInt(GetVal(row, "开启同步", 12), 0) == 1,
                                    SyncTargetDeviceId = ParseInt(GetVal(row, "同步目标设备ID", 13), -1) == -1 ? null : ParseInt(GetVal(row, "同步目标设备ID", 13), -1),
                                    SyncTargetAddress = ParseInt(GetVal(row, "同步写入地址", 14), -1) == -1 ? null : (ushort?)ParseInt(GetVal(row, "同步写入地址", 14), -1),
                                    SyncTargetBitIndex = ParseInt(GetVal(row, "同步目标位索引", 15), -1) == -1 ? null : ParseInt(GetVal(row, "同步目标位索引", 15), -1),
                                    SyncMode = ParseInt(GetVal(row, "同步模式", 16), 0) ?? 0,
                                    IsLogEnabled = ParseInt(GetVal(row, "开启日志", 17), 0) == 1,
                                    LogTypeId = ParseInt(GetVal(row, "日志类型ID", 18), 1) ?? 1,
                                    LogTriggerState = ParseInt(GetVal(row, "日志触发策略", 19), 2) ?? 2,
                                    LogDeadband = ParseDouble(GetVal(row, "日志死区", 20)),
                                    LogMessage = GetVal(row, "日志内容模板", 21)?.ToString(),
                                    Category = GetVal(row, "日志分类", 22)?.ToString(),
                                    HighLimit = ParseDouble(GetVal(row, "高限报警", 23)),
                                    LowLimit = ParseDouble(GetVal(row, "低限报警", 24)),
                                    State0Desc = GetVal(row, "状态0描述", 25)?.ToString(),
                                    State1Desc = GetVal(row, "状态1描述", 26)?.ToString(),
                                    AlarmTargetValue = ParseInt(GetVal(row, "报警目标值", 27), -1) == -1 ? null : ParseInt(GetVal(row, "报警目标值", 27), -1)
                                };

                                if (existing != null)
                                {
                                    // 检查是否有实质性更改 (全字段严格比对)
                                    bool isChanged = 
                                        !StringEquals(entity.Address, existing.Address) ||
                                        entity.BitIndex != existing.BitIndex ||
                                        !StringEquals(entity.DataType, existing.DataType) ||
                                        !StringEquals(entity.Description, existing.Description) ||
                                        !StringEquals(entity.UiBinding, existing.UiBinding) ||
                                        !StringEquals(entity.BindingRole, existing.BindingRole) ||
                                        entity.TargetType != existing.TargetType ||
                                        !StringEquals(entity.TargetKeyId, existing.TargetKeyId) ||
                                        !StringEquals(entity.TargetBitConfigKeyId, existing.TargetBitConfigKeyId) ||
                                        entity.IsSyncEnabled != existing.IsSyncEnabled ||
                                        !IntEquals(entity.SyncTargetDeviceId, existing.SyncTargetDeviceId) ||
                                        !IntEquals((int?)entity.SyncTargetAddress, (int?)existing.SyncTargetAddress) ||
                                        !IntEquals(entity.SyncTargetBitIndex, existing.SyncTargetBitIndex) ||
                                        entity.SyncMode != existing.SyncMode ||
                                        entity.IsLogEnabled != existing.IsLogEnabled ||
                                        entity.LogTypeId != existing.LogTypeId ||
                                        !StringEquals(entity.LogMessage, existing.LogMessage) ||
                                        !StringEquals(entity.Category, existing.Category) ||
                                        !DoubleEquals(entity.HighLimit, existing.HighLimit) ||
                                        !DoubleEquals(entity.LowLimit, existing.LowLimit) ||
                                        !StringEquals(entity.State0Desc, existing.State0Desc) ||
                                        !StringEquals(entity.State1Desc, existing.State1Desc) ||
                                        !IntEquals(entity.AlarmTargetValue, existing.AlarmTargetValue) ||
                                        entity.FunctionCode != existing.FunctionCode ||
                                        entity.LogTriggerState != existing.LogTriggerState ||
                                        !DoubleEquals(entity.LogDeadband, existing.LogDeadband);

                                    if (isChanged)
                                    {
                                        if (updateCount < 5)
                                        {
                                            var sb = new System.Text.StringBuilder();
                                            sb.Append($"[差异记录] 点位 {entity.Address} (ID: {existing.Id}): ");
                                            if (!StringEquals(entity.DataType, existing.DataType)) sb.Append($"类型[{existing.DataType} -> {entity.DataType}] ");
                                            if (!StringEquals(entity.Description, existing.Description)) sb.Append($"描述[{existing.Description} -> {entity.Description}] ");
                                            if (entity.IsSyncEnabled != existing.IsSyncEnabled) sb.Append($"同步[{existing.IsSyncEnabled} -> {entity.IsSyncEnabled}] ");
                                            if (!IntEquals(entity.SyncTargetDeviceId, existing.SyncTargetDeviceId)) sb.Append($"同步目标ID[{existing.SyncTargetDeviceId} -> {entity.SyncTargetDeviceId}] ");
                                            if (!IntEquals(entity.AlarmTargetValue, existing.AlarmTargetValue)) sb.Append($"报警值[{existing.AlarmTargetValue} -> {entity.AlarmTargetValue}] ");
                                            if (entity.FunctionCode != existing.FunctionCode) sb.Append($"功能码[{existing.FunctionCode} -> {entity.FunctionCode}] ");
                                            if (entity.LogTriggerState != existing.LogTriggerState) sb.Append($"日志策略[{existing.LogTriggerState} -> {entity.LogTriggerState}] ");
                                            if (!DoubleEquals(entity.LogDeadband, existing.LogDeadband)) sb.Append($"日志死区[{existing.LogDeadband} -> {entity.LogDeadband}] ");
                                            LogHelper.Info(sb.ToString());
                                        }

                                        entity.Id = existing.Id;
                                        db.Update(entity);
                                        updateCount++;
                                    }
                                    else
                                    {
                                        skipCount++;
                                    }
                                }
                                else
                                {
                                    db.Insert(entity);
                                    addCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errorCount++;
                                if (errorCount <= 5) errorDetails.Add($"行数据解析失败: {ex.Message}");
                            }
                        }
                        db.CommitTransaction();

                        if (errorDetails.Any())
                        {
                            LogHelper.Warn($"[Excel导入] 过程中发生了 {errorCount} 处解析问题，样本: {string.Join(" | ", errorDetails)}");
                        }
                        LogHelper.Info($"[Excel导入] 设备 [{SelectedDevice.Name}] 导入任务完成: 新增={addCount}, 更新={updateCount}, 未变跳过={skipCount}, 失败={errorCount}。");
                    }
                    catch (Exception ex)
                    {
                        db.RollbackTransaction();
                        LogHelper.Error($"[Excel导入] 导入过程中止并回滚: {ex.Message}", ex);
                        throw new Exception($"导入过程发生异常，已回滚事务: {ex.Message}", ex);
                    }

                    // 显示详细的处理汇总
                    MessageBox.Show($"点位导入任务已完成。\n\n" +
                                    $"新增点位: {addCount} 条\n" +
                                    $"更新点位: {updateCount} 条\n" +
                                    $"跳过(无变化): {skipCount} 条\n" +
                                    $"处理失败: {errorCount} 条\n\n" +
                                    $"系统已根据导入的配置完成更新。", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadPoints(); 
                    DeviceCommunicationService.Instance?.ReloadConfigs();
                }
                catch (Exception ex)
                {
                    LogHelper.Error($"[Excel导入] 严重错误: {ex.Message}", ex);
                    MessageBox.Show($"导入严重失败: {ex.Message}", "系统错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
