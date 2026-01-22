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
            AddCommand = new RelayCommand(AddPath);
            DeleteCommand = new RelayCommand(DeletePath);
            ClearCommand = new RelayCommand(ClearPaths);
            SaveCommand = new RelayCommand(SavePaths);
            SelectCommand = new RelayCommand(SelectPath);
            TextChangedCommand = new RelayCommand(OnTextChanged);
            ExportDictionaryCommand = new RelayCommand(ExportGraphicDictionary);
            ImportDictionaryCommand = new RelayCommand(ImportGraphicDictionary);



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

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SaveCommand { get; } 
        public ICommand ExportDictionaryCommand { get; }
        public ICommand ImportDictionaryCommand { get; }
        public ICommand SelectCommand { get; }
        public ICommand TextChangedCommand { get; }

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
                MessageBox.Show("图形名称不能为空！");
                return;
            }

            if (GlobalData.GraphicDictionary.ContainsKey(IconName))
            {
                MessageBox.Show("图形名称存在冲突，请修改后保存！");
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

            PathItems.Clear();
        }


        // 图形点击时设置为选中
        private void SelectPath(object obj)
        {
            if (obj is IconItem item)
            {
                foreach (var i in PathItems)
                    i.IsSelected = false;

                item.IsSelected = true;
                SelectedItem = item;
            }
        }

        // 导出图形到 JSON 文件
        private void ExportGraphicDictionary(object obj)
        {
            try
            {
                string directory = AppDomain.CurrentDomain.BaseDirectory; // 程序运行目录
                string filePath = Path.Combine(directory, "GraphicDictionary.json");

                var exportData = GlobalData.GraphicDictionary.Select(kvp => new SerializableGraphicGroup
                {
                    Name = kvp.Key,
                    Items = kvp.Value.Select(i => new SerializableIconItem
                    {
                        PathData = i.Data?.ToString(),
                        StrokeColor = ((SolidColorBrush)i.Stroke).Color.ToString(),
                        FillColor = ((SolidColorBrush)i.Fill).Color.ToString(),
                        StrokeThickness = i.StrokeThickness
                    }).ToList()
                }).ToList();

                //var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // ✅ 保留中文字符
                });
                File.WriteAllText(filePath, json);

                MessageBox.Show($"图形字典已成功导出到：\n{filePath}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        GlobalData.GraphicDictionary.Clear();
                        GraphicGroups.Clear();

                        foreach (var group in imported)
                        {
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

                            GlobalData.GraphicDictionary[group.Name] = itemList;
                            GraphicGroups.Add(new GraphicGroup { Name = group.Name, Items = itemList });
                        }

                        MessageBox.Show("图形字典导入成功！");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败：{ex.Message}");
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


/*

    M172.935 139.173h301.368v763.674H141.451v-732.19a31.69 31.69 0 0 1 31.484-31.484z m709.614 763.674H549.697V139.173h301.368a31.69 31.69 0 0 1 31.69 31.69v731.984h-0.206z  
    M300 120h430v800H300z 
    M97.121 855.518h829.758q22.028 0 22.028 22.027v51.519q0 22.027-22.028 22.027H97.121q-22.028 0-22.028-22.027v-51.519q0-22.027 22.028-22.027z 
    M211.307 532.48h10.72q17.12 0 17.12 17.12v10.72q0 17.12-17.12 17.12h-10.72q-17.12 0-17.12-17.12V549.6q0-17.12 17.12-17.12zM801.973 532.48h10.72q17.12 0 17.12 17.12v10.72q0 17.12-17.12 17.12h-10.72q-17.12 0-17.12-17.12V549.6q0-17.12 17.12-17.12z 

     */



