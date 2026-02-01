using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace DoorMonitorSystem.UControl
{
    public partial class DateTimePicker : UserControl, INotifyPropertyChanged
    {
        private DateTime _originalDateTime;

        public static readonly DependencyProperty SelectedDateTimeProperty =
            DependencyProperty.Register("SelectedDateTime", typeof(DateTime), typeof(DateTimePicker),
                new FrameworkPropertyMetadata(DateTime.Now, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDateTimeChanged));

        public static readonly DependencyProperty MinDateTimeProperty =
            DependencyProperty.Register("MinDateTime", typeof(DateTime), typeof(DateTimePicker), new PropertyMetadata(DateTime.MinValue));

        public static readonly DependencyProperty MaxDateTimeProperty =
            DependencyProperty.Register("MaxDateTime", typeof(DateTime), typeof(DateTimePicker), new PropertyMetadata(DateTime.MaxValue));

        public DateTime SelectedDateTime
        {
            get => (DateTime)GetValue(SelectedDateTimeProperty);
            set 
            {
                // 约束逻辑：确保在最小值和最大值之间
                DateTime clamped = value;
                if (clamped < MinDateTime) clamped = MinDateTime;
                if (clamped > MaxDateTime) clamped = MaxDateTime;
                SetValue(SelectedDateTimeProperty, clamped); 
            }
        }

        public DateTime MinDateTime
        {
            get => (DateTime)GetValue(MinDateTimeProperty);
            set => SetValue(MinDateTimeProperty, value);
        }

        public DateTime MaxDateTime
        {
            get => (DateTime)GetValue(MaxDateTimeProperty);
            set => SetValue(MaxDateTimeProperty, value);
        }

        public ObservableCollection<int> Hours { get; } = new ObservableCollection<int>(Enumerable.Range(0, 24));
        public ObservableCollection<int> Minutes { get; } = new ObservableCollection<int>(Enumerable.Range(0, 60));
        public ObservableCollection<int> Seconds { get; } = new ObservableCollection<int>(Enumerable.Range(0, 60));

        public int SelectedHour
        {
            get => SelectedDateTime.Hour;
            set
            {
                var dt = SelectedDateTime;
                SelectedDateTime = new DateTime(dt.Year, dt.Month, dt.Day, value, dt.Minute, dt.Second);
                OnPropertyChanged();
            }
        }

        public int SelectedMinute
        {
            get => SelectedDateTime.Minute;
            set
            {
                var dt = SelectedDateTime;
                SelectedDateTime = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, value, dt.Second);
                OnPropertyChanged();
            }
        }

        public int SelectedSecond
        {
            get => SelectedDateTime.Second;
            set
            {
                var dt = SelectedDateTime;
                SelectedDateTime = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, value);
                OnPropertyChanged();
            }
        }

        public DateTime PickerDate
        {
            get => SelectedDateTime.Date;
            set
            {
                var dt = SelectedDateTime;
                var newDt = new DateTime(value.Year, value.Month, value.Day, dt.Hour, dt.Minute, dt.Second);
                
                // 同样进行约束校验
                if (newDt < MinDateTime) newDt = MinDateTime;
                if (newDt > MaxDateTime) newDt = MaxDateTime;

                SelectedDateTime = newDt;
                OnPropertyChanged();
            }
        }

        public DateTimePicker()
        {
            InitializeComponent();
        }

        private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DateTimePicker picker)
            {
                picker.OnPropertyChanged(nameof(SelectedHour));
                picker.OnPropertyChanged(nameof(SelectedMinute));
                picker.OnPropertyChanged(nameof(SelectedSecond));
                picker.OnPropertyChanged(nameof(PickerDate));
            }
        }

        private void OnNowClick(object sender, RoutedEventArgs e)
        {
            SelectedDateTime = DateTime.Now;
            PopupElement.IsOpen = false;
            HeaderButton.Focus();
            e.Handled = true;
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            PopupElement.IsOpen = false;
            HeaderButton.Focus();
            e.Handled = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            SelectedDateTime = _originalDateTime;
            PopupElement.IsOpen = false;
            HeaderButton.Focus();
            e.Handled = true;
        }

        private void OnReturnToMonthClick(object sender, RoutedEventArgs e)
        {
            // 通过查找父级 Calendar 控件重置显示模式
            DependencyObject parent = sender as DependencyObject;
            while (parent != null && !(parent is Calendar))
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }

            if (parent is Calendar calendar)
            {
                calendar.DisplayMode = CalendarMode.Month;
            }
        }

        private void OnHeaderClick(object sender, RoutedEventArgs e)
        {
            // 仅在打开时记录原始值，用于取消操作
            if (PopupElement.IsOpen)
            {
                _originalDateTime = SelectedDateTime;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
