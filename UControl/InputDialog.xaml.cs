 using System;
using System.Reflection.Metadata;
using System.Security.Claims;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Linq;
using DoorMonitorSystem.UControl;
using System.Collections.Generic;

namespace DoorMonitorSystem.UControl
{
  
    public partial class InputDialog : Window
    {


        // 用于存储用户输入的文本
        public string InputText { get; set; }

        // 构造函数，接收标题和提示信息作为参数
        public InputDialog(string title, string prompt)
        {
            InitializeComponent();
            Title = title;
            lblPrompt.Content = prompt;
        }

        // 确定按钮点击事件处理程序
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            // 获取文本输入框中的文本
            InputText = txtInput.Text;
            // 设置对话框结果为 true，表示用户点击了确定
            DialogResult = true;
            // 关闭对话框
            Close();
        }

        // 取消按钮点击事件处理程序
        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            // 设置对话框结果为 false，表示用户点击了取消
            DialogResult = false;
            // 关闭对话框
            Close();
        }

        private void txtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 手动触发确定逻辑
                btnOk_Click(sender, e);
            }
        }

    }
}


