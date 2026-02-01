using System.Windows;
using System.Windows.Controls;

namespace DoorMonitorSystem.UControl
{
    /// <summary>
    /// SettingItem.xaml 的交互逻辑
    /// </summary>
    public partial class SettingItem : UserControl
    {
        public SettingItem()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(SettingItem), new PropertyMetadata("参数名称:"));

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(string), typeof(SettingItem), 
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Value
        {
            get { return (string)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public static readonly DependencyProperty ReadbackValueProperty =
            DependencyProperty.Register("ReadbackValue", typeof(string), typeof(SettingItem), new PropertyMetadata(string.Empty));

        public string ReadbackValue
        {
            get { return (string)GetValue(ReadbackValueProperty); }
            set { SetValue(ReadbackValueProperty, value); }
        }

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register("Unit", typeof(string), typeof(SettingItem), new PropertyMetadata(string.Empty));

        public string Unit
        {
            get { return (string)GetValue(UnitProperty); }
            set { SetValue(UnitProperty, value); }
        }

        public static readonly DependencyProperty HintProperty =
            DependencyProperty.Register("Hint", typeof(string), typeof(SettingItem), new PropertyMetadata(string.Empty));

        public string Hint
        {
            get { return (string)GetValue(HintProperty); }
            set { SetValue(HintProperty, value); }
        }

        public static readonly DependencyProperty IsEditableProperty =
            DependencyProperty.Register("IsEditable", typeof(bool), typeof(SettingItem), new PropertyMetadata(true));

        public bool IsEditable
        {
            get { return (bool)GetValue(IsEditableProperty); }
            set { SetValue(IsEditableProperty, value); }
        }

        #endregion
    }
}
