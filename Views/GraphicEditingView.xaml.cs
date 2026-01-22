using ControlLibrary.Models;
using DoorMonitorSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.XPath;

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// GraphicEditingView.xaml 的交互逻辑
    /// </summary>
    public partial class GraphicEditingView : UserControl
    {
        public GraphicEditingView()
        {
            InitializeComponent();
            this.DataContext = new GraphicEditingViewModel();

        }

        /// <summary>
        /// 插入矩形 Path 模板
        /// </summary>
        private void InsertRectangle_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GraphicEditingViewModel vm)
            {
                vm.PathDataText = "M 100,100 L 900,100 L 900,900 L 100,900 Z";
            }
        }

        /// <summary>
        /// 插入圆形 Path 模板
        /// </summary>
        private void InsertCircle_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GraphicEditingViewModel vm)
            {
                vm.PathDataText = "M 512,100 A 412,412 0 1,1 512,924 A 412,412 0 1,1 512,100 Z";
            }
        }

        /// <summary>
        /// 插入三角形 Path 模板
        /// </summary>
        private void InsertTriangle_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GraphicEditingViewModel vm)
            {
                vm.PathDataText = "M 512,100 L 900,900 L 124,900 Z";
            }
        }

        /// <summary>
        /// 插入箭头 Path 模板
        /// </summary>
        private void InsertArrow_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GraphicEditingViewModel vm)
            {
                vm.PathDataText = "M 100,512 L 700,512 L 700,300 L 924,512 L 700,724 L 700,512 Z";
            }
        }

        /// <summary>
        /// 插入门框 Path 模板
        /// </summary>
        private void InsertDoorFrame_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is GraphicEditingViewModel vm)
            {
                vm.PathDataText = "M 200,100 L 824,100 L 824,924 L 200,924 Z M 300,200 L 724,200 L 724,824 L 300,824 Z";
            }
        }
    }
}
