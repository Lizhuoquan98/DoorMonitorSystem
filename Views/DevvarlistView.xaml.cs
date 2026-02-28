﻿using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
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
using System.Windows.Shapes;

namespace DoorMonitorSystem.Views
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class DevvarlistView : UserControl
    {
      
        public DevvarlistView()
        { 
            InitializeComponent();    
        }

        private bool _isDragging = false;
        private Point _startPoint;
        private TranslateTransform? _transform;

        private void ConfigWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as FrameworkElement;
            if (border != null)
            {
                _isDragging = true;
                _startPoint = e.GetPosition(this);
                if (border.RenderTransform is not TranslateTransform)
                {
                    border.RenderTransform = new TranslateTransform();
                }
                _transform = (TranslateTransform)border.RenderTransform;
                border.CaptureMouse();
            }
        }

        private void ConfigWindow_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _transform != null)
            {
                var currentPoint = e.GetPosition(this);
                var offset = currentPoint - _startPoint;
                _transform.X += offset.X;
                _transform.Y += offset.Y;
                _startPoint = currentPoint;
            }
        }

        private void ConfigWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                var border = sender as FrameworkElement;
                border?.ReleaseMouseCapture();
            }
        }
    }
}
