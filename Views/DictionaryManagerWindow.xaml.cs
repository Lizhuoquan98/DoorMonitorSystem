using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DoorMonitorSystem.Views
{
    public partial class DictionaryManagerWindow : Window
    {
        private string _type;
        private LayoutConfigurationViewModel _mainVm;

        public DictionaryManagerWindow(string type, LayoutConfigurationViewModel mainVm)
        {
            InitializeComponent();
            _type = type;
            _mainVm = mainVm;

            SetupUI();
        }

        private void SetupUI()
        {
            if (_type == "Category")
            {
                this.Width = 850; // 类别列多，宽一点
                TitleText.Text = "点位分类管理 (BitCategory)";
                MainGrid.ItemsSource = _mainVm.BitCategories;

                MainGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("Id") { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = new DataGridLength(40) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "代码", Binding = new Binding("Code"), Width = new DataGridLength(80) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "名称", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "图标", Binding = new Binding("Icon"), Width = new DataGridLength(50) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "背景色", Binding = new Binding("BackgroundColor"), Width = new DataGridLength(80) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "前景色", Binding = new Binding("ForegroundColor"), Width = new DataGridLength(80) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "排序", Binding = new Binding("SortOrder"), Width = new DataGridLength(50) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "布局行", Binding = new Binding("LayoutRows"), Width = new DataGridLength(60) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "布局列", Binding = new Binding("LayoutColumns"), Width = new DataGridLength(60) });
            }
            else if (_type == "Color")
            {
                this.Width = 550; // 颜色列少，窄一点
                TitleText.Text = "颜色字典管理 (BitColor)";
                MainGrid.ItemsSource = _mainVm.BitColors;

                MainGrid.Columns.Add(new DataGridTextColumn { Header = "ID", Binding = new Binding("Id") { Mode = BindingMode.OneWay }, IsReadOnly = true, Width = new DataGridLength(40) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "颜色名称", Binding = new Binding("ColorName"), Width = new DataGridLength(120) });
                MainGrid.Columns.Add(new DataGridTextColumn { Header = "十六进制值", Binding = new Binding("ColorValue"), Width = new DataGridLength(120) });
                
                // 动态创建颜色预览列模板
                var templateColumn = new DataGridTemplateColumn
                {
                    Header = "颜色预览",
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                };
                
                try 
                {
                    var xaml = @"<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">
                                    <Border Background=""{Binding ColorValue}"" Width=""100"" Height=""16"" HorizontalAlignment=""Left"" CornerRadius=""3"" Margin=""10,4"" BorderBrush=""#66FFFFFF"" BorderThickness=""1""/>
                                 </DataTemplate>";
                    templateColumn.CellTemplate = (DataTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
                }
                catch { /* 忽略模板解析错误 */ }

                MainGrid.Columns.Add(templateColumn);
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (_type == "Category")
            {
                _mainVm.BitCategories.Add(new BitCategoryEntity { Name = "新分类", Code = "New", SortOrder = _mainVm.BitCategories.Count + 1, LayoutColumns = 2 });
            }
            else if (_type == "Color")
            {
                _mainVm.BitColors.Add(new BitColorEntity { ColorName = "新颜色", ColorValue = "#FFFFFF" });
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = MainGrid.SelectedItems.Cast<object>().ToList();
            if (selectedItems.Count == 0) return;

            if (MessageBox.Show($"确定要删除选中的 {selectedItems.Count} 项吗？", "确认", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();

                    foreach (var item in selectedItems)
                    {
                        if (_type == "Category" && item is BitCategoryEntity cat)
                        {
                            if (cat.Id != 0) db.Delete(cat);
                            _mainVm.BitCategories.Remove(cat);
                        }
                        else if (_type == "Color" && item is BitColorEntity col)
                        {
                            if (col.Id != 0) db.Delete(col);
                            _mainVm.BitColors.Remove(col);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败: {ex.Message}");
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();

                if (_type == "Category")
                {
                    // 简单同步：先清空再重新插入，或者更新已有项
                    // 为了简单和稳妥，我们直接使用 SQLHelper 的常用方法
                    // 实际项目中建议使用更精细的同步逻辑
                    
                    foreach (var item in _mainVm.BitCategories)
                    {
                        if (item.Id == 0)
                            db.Insert(item);
                        else
                            db.Update(item);
                    }
                }
                else if (_type == "Color")
                {
                    foreach (var item in _mainVm.BitColors)
                    {
                        if (item.Id == 0)
                            db.Insert(item);
                        else
                            db.Update(item);
                    }
                }

                MessageBox.Show("保存成功！");
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
