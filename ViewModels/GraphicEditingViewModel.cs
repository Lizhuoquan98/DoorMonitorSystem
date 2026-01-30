using Base;
using ControlLibrary.Models;
using DoorMonitorSystem.Base;
using Google.Protobuf.Compiler;
using Microsoft.Win32;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.system;

namespace DoorMonitorSystem.ViewModels
{
    // 每一组图形（名称 + 图形集合）
    public class GraphicGroup
    {
        public string Name { get; set; }
        public List<IconItem> Items { get; set; }
    }
    public class SerializableGraphicGroup
    {
        public string Name { get; set; }
        public List<SerializableIconItem> Items { get; set; }
    }

    public class SerializableIconItem
    {
        public string PathData { get; set; }
        public string StrokeColor { get; set; }
        public string FillColor { get; set; }
        public double StrokeThickness { get; set; }
    }


    public class GraphicEditingViewModel : NotifyPropertyChanged
    {
        public GraphicEditingViewModel()
        {
            // 初始化命令
            SaveCommand = new RelayCommand(SavePaths);
            ExportDictionaryCommand = new RelayCommand(SaveDictionaryToDatabase);
            ImportDictionaryCommand = new RelayCommand(ImportGraphicDictionary);
            ImportSvgCommand = new RelayCommand(ImportSvgFile);
            DeleteGraphicCommand = new RelayCommand(DeleteGraphic);  // 删除图形命令

            // 从静态 GraphicDictionary 构造分组视图数据
            if (GlobalData.GraphicDictionary != null)
            {
                foreach (var kvp in GlobalData.GraphicDictionary)
                {
                    GraphicGroups.Add(new GraphicGroup
                    {
                        Name = kvp.Key,
                        Items = kvp.Value
                    });
                }
            }

        }

      
        // 图形集合
        public ObservableCollection<IconItem> PathItems { get; } = new();

        // 当前选中图形
        private IconItem _selectedItem;
        public IconItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    _selectedItem = value;
                    OnPropertyChanged();

                    if (_selectedItem != null)
                    {
                        PathData = _selectedItem.Data;
                        PathDataText = _selectedItem.Data?.ToString();
                        StrokeColor = (_selectedItem.Stroke as SolidColorBrush)?.Color ?? Colors.Black;
                        FillColor = (_selectedItem.Fill as SolidColorBrush)?.Color ?? Colors.Transparent;
                        Thickness = _selectedItem.StrokeThickness;
                    }
                }
            }
        }

        // 图形路径字符串
        private string _pathDataText;
        public string PathDataText
        {
            get => _pathDataText;
            set
            {
                if (_pathDataText != value)
                {
                    _pathDataText = value;
                    OnPropertyChanged();
                }
            }
        }

        // 几何数据（解析后的 PathData）
        private Geometry _pathData;
        public Geometry PathData
        {
            get => _pathData;
            set
            {
                _pathData = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsPreviewValid));
            }
        }

        public ObservableCollection<GraphicGroup> GraphicGroups { get; } = []; 

        public bool IsPreviewValid => PathData != null;

        private Color _strokeColor = Colors.Black;
        public Color StrokeColor
        {
            get => _strokeColor;
            set { _strokeColor = value; OnPropertyChanged(); OnPropertyChanged(nameof(StrokeBrush)); }
        }

        private Color _fillColor = Colors.Transparent;
        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; OnPropertyChanged(); OnPropertyChanged(nameof(FillBrush)); }
        }

        private double _thickness = 1;
        public double Thickness
        {
            get => _thickness;
            set { _thickness = value; OnPropertyChanged(); }
        }

        public Brush StrokeBrush => new SolidColorBrush(StrokeColor);
        public Brush FillBrush => new SolidColorBrush(FillColor);

        private string _iconName = "1";
        public string IconName
        {
            get => _iconName;
            set { _iconName = value; OnPropertyChanged(); }
        }

        // ------------------- 命令定义 -------------------

        public ICommand SaveCommand { get; }
        public ICommand ExportDictionaryCommand { get; }
        public ICommand ImportDictionaryCommand { get; }
        public ICommand ImportSvgCommand { get; }  // 导入 SVG 命令
        public ICommand DeleteGraphicCommand { get; }  // 删除图形命令

        // 添加图形命令
        private void AddPath(object obj)
        {
            if (PathData == null) return;

            var item = new IconItem
            {
                Data = PathData,
                Stroke = new SolidColorBrush(StrokeColor),
                Fill = new SolidColorBrush(FillColor),
                StrokeThickness = Thickness,
                IsSelected = false
            };
            PathItems.Add(item);
        }

        // 删除选中图形
        private void DeletePath(object obj)
        {
            if (SelectedItem != null)
                PathItems.Remove(SelectedItem);
        }

        // 清空图形
        private void ClearPaths(object obj)
        {
            PathItems.Clear();
            SelectedItem = null;
        }
 
        // 保存图形到全局字典
        private void SavePaths(object obj)
        {
            if (string.IsNullOrWhiteSpace(IconName))
            {
                MessageBox.Show("图标标识不能为空！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PathItems.Count == 0)
            {
                MessageBox.Show("请先加载 SVG 文件！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (GlobalData.GraphicDictionary.ContainsKey(IconName))
            {
                MessageBox.Show("该标识已存在，请修改后保存！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 创建 PathItems 的快照副本，防止后续修改影响已保存项
            var snapshot = new List<IconItem>(
                PathItems.Select(item => item.Clone())
            );

            GlobalData.GraphicDictionary.Add(IconName, snapshot);

            GraphicGroups.Add(new GraphicGroup
            {
                Name = IconName,
                Items = snapshot
            });

            MessageBox.Show($"组件 \"{IconName}\" 已成功存入库中！", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);

            // 清空输入
            PathItems.Clear();
            IconName = "";
        }

        /// <summary>
        /// 删除图形从字典
        /// </summary>
        private void DeleteGraphic(object obj)
        {
            if (obj is string graphicName)
            {
                var result = MessageBox.Show(
                    $"确定要删除图形 \"{graphicName}\" 吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    // 从全局字典中删除
                    GlobalData.GraphicDictionary.Remove(graphicName);

                    // 从 UI 列表中删除
                    var groupToRemove = GraphicGroups.FirstOrDefault(g => g.Name == graphicName);
                    if (groupToRemove != null)
                    {
                        GraphicGroups.Remove(groupToRemove);
                    }

                    MessageBox.Show($"组件 \"{graphicName}\" 已成功删除！", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        // 保存图形字典到数据库
        private void SaveDictionaryToDatabase(object obj)
        {
            if (MessageBox.Show("确定要将当前组件库同步到数据库吗？这将会覆盖数据库中的原有配置。", "确认同步", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            if (GlobalData.SysCfg == null)
            {
                MessageBox.Show("系统配置未加载，无法连接数据库", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();
                db.BeginTransaction();

                try
                {
                    // 1. 清空旧数据
                    db.ExecuteNonQuery("DELETE FROM SysGraphicItemEntity");
                    db.ExecuteNonQuery("DELETE FROM SysGraphicGroupEntity");

                    // 2. 插入新数据
                    foreach (var kvp in GlobalData.GraphicDictionary)
                    {
                        string groupName = kvp.Key;
                        var items = kvp.Value;

                        // Insert Group
                        db.ExecuteNonQuery("INSERT INTO SysGraphicGroupEntity (GroupName) VALUES (@Name)", new MySql.Data.MySqlClient.MySqlParameter("@Name", groupName));
                        
                        // Get ID
                        var idObj = db.ExecuteScalar("SELECT LAST_INSERT_ID()");
                        int groupId = Convert.ToInt32(idObj);

                        // Insert Items
                        int sortIndex = 0;
                        foreach (var item in items)
                        {
                            var itemEntity = new SysGraphicItemEntity
                            {
                                GroupId = groupId,
                                PathData = item.Data?.ToString() ?? "",
                                StrokeColor = ((SolidColorBrush)item.Stroke).Color.ToString(),
                                FillColor = ((SolidColorBrush)item.Fill).Color.ToString(),
                                StrokeThickness = item.StrokeThickness,
                                SortIndex = sortIndex++
                            };
                            db.Insert(itemEntity, "SysGraphicItemEntity");
                        }
                    }

                    db.CommitTransaction();
                    MessageBox.Show("组件库已成功同步至数据库！", "同步成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {
                    db.RollbackTransaction();
                    throw;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 导入图形从 JSON 文件
        public void ImportGraphicDictionary(object obj)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "JSON 文件|*.json"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(openDialog.FileName);
                    var imported = JsonSerializer.Deserialize<List<SerializableGraphicGroup>>(json);

                    if (imported != null)
                    {
                        // GlobalData.GraphicDictionary.Clear(); // 移除清空操作，改为追加模式
                        // GraphicGroups.Clear();

                        int importedCount = 0;
                        foreach (var group in imported)
                        {
                            // 冲突检测与重命名策略
                            string uniqueName = group.Name;
                            int suffix = 1;
                            while (GlobalData.GraphicDictionary.ContainsKey(uniqueName))
                            {
                                uniqueName = $"{group.Name}_{suffix}";
                                suffix++;
                            }

                            var itemList = new List<IconItem>();
                            foreach (var item in group.Items)
                            {
                                itemList.Add(new IconItem
                                {
                                    Data = Geometry.Parse(item.PathData),
                                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.StrokeColor)),
                                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.FillColor)),
                                    StrokeThickness = item.StrokeThickness,
                                    IsSelected = false
                                });
                            }

                            GlobalData.GraphicDictionary[uniqueName] = itemList;
                            GraphicGroups.Add(new GraphicGroup { Name = uniqueName, Items = itemList });
                            importedCount++;
                        }

                        MessageBox.Show($"组件库导入成功！已追加导入 {importedCount} 组组件。\n如有重名已自动重命名。", "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导入 SVG 文件并解析为图形
        /// </summary>
        private void ImportSvgFile(object obj)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "SVG 文件|*.svg|所有文件|*.*",
                Title = "选择 SVG 文件"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    // 读取 SVG 文件内容（使用 UTF-8 编码）
                    string svgContent = File.ReadAllText(openDialog.FileName, System.Text.Encoding.UTF8);

                    // 使用 SvgParser 解析 SVG
                    var svgPaths = Assets.Helper.SvgParser.ParseSvgFile(svgContent);

                    if (svgPaths == null || svgPaths.Count == 0)
                    {
                        MessageBox.Show("未能从 SVG 文件中解析出有效的矢量路径。", "加载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 清空当前画布
                    PathItems.Clear();

                    // 将解析出的 Path 添加到画布
                    int successCount = 0;
                    foreach (var svgPath in svgPaths)
                    {
                        try
                        {
                            // 直接使用原始路径数据
                            string pathData = svgPath.PathData;

                            // 规范化 Path Data（去除多余空格）
                            pathData = Assets.Helper.SvgParser.NormalizePathData(pathData);

                            // 解析几何数据
                            var geometry = Geometry.Parse(pathData);

                            // ★ 关键修复：应用 Transform 并立即烘焙到几何中
                            if (!string.IsNullOrEmpty(svgPath.Transform))
                            {
                                var translateMatch = System.Text.RegularExpressions.Regex.Match(
                                    svgPath.Transform,
                                    @"translate\(([^,\s]+)[\s,]+([^)]+)\)"
                                );

                                if (translateMatch.Success)
                                {
                                    if (double.TryParse(translateMatch.Groups[1].Value, out double tx) &&
                                        double.TryParse(translateMatch.Groups[2].Value, out double ty))
                                    {
                                        // 创建带变换的几何图形
                                        var transformedGeometry = geometry.Clone();
                                        transformedGeometry.Transform = new TranslateTransform(tx, ty);
                                        
                                        // ★ 使用 GetFlattenedPathGeometry 将变换烘焙到路径坐标中
                                        geometry = transformedGeometry.GetFlattenedPathGeometry();
                                    }
                                }
                            }

                            // 记录边界框信息
                            string boundsInfo = Assets.Helper.SvgParser.GetGeometryBoundsInfo(geometry);

                            // 解析颜色
                            var fillBrush = Assets.Helper.SvgParser.ParseColorToBrush(svgPath.Fill);
                            var strokeBrush = Assets.Helper.SvgParser.ParseColorToBrush(svgPath.Stroke);

                            // 创建 IconItem - 符合门控系统要求的格式
                            var iconItem = new IconItem
                            {
                                Data = geometry,  // 现在 geometry 已经包含了绝对坐标，没有 Transform 属性
                                Fill = fillBrush,
                                Stroke = strokeBrush,
                                StrokeThickness = svgPath.StrokeThickness,
                                IsSelected = false
                                // Id 会自动生成 GUID
                            };

                            PathItems.Add(iconItem);
                            successCount++;

                            // 输出转换日志（用于调试）
                            System.Diagnostics.Debug.WriteLine(
                                $"✓ IconItem #{successCount} 转换成功:\n" +
                                $"  - PathData 长度: {pathData.Length} 字符\n" +
                                $"  - Transform: {(string.IsNullOrEmpty(svgPath.Transform) ? "none" : svgPath.Transform)}\n" +
                                $"  - 边界框: {boundsInfo}\n" +
                                $"  - Fill: {svgPath.Fill}\n" +
                                $"  - Stroke: {svgPath.Stroke}\n" +
                                $"  - ID: {iconItem.Id}"
                            );
                        }
                        catch (Exception ex)
                        {
                            // 跳过无效的 Path，继续处理其他的
                            System.Diagnostics.Debug.WriteLine($"✗ 解析 Path 失败: {ex.Message}\n{ex.StackTrace}");
                        }
                    }

                    MessageBox.Show(
                        $"SVG 加载成功！\n\n" +
                        $"文件: {Path.GetFileName(openDialog.FileName)}\n" +
                        $"成功导入: {successCount} 个图形元素\n" +
                        $"总共发现: {svgPaths.Count} 个 Path 元素",
                        "导入成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"SVG 文件导入失败！\n\n错误信息:\n{ex.Message}",
                        "导入失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }




        // TextChanged → 解析 Geometry
        private void OnTextChanged(object obj)
        {
            if (obj is string path)
            {
                try
                {
                    PathData = Geometry.Parse(path.Trim());
                }
                catch
                {
                    PathData = null;
                }
            }
        }
    }
}

 
