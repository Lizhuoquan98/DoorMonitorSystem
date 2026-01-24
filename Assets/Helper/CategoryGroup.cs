using DoorMonitorSystem.Models.RunModels;
using DoorMonitorSystem.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace DoorMonitorSystem.Assets.Helper
{
    /// <summary>
    /// 分类分组辅助类（用于弹窗按分类显示点位）
    /// </summary>
    public class CategoryGroup : NotifyPropertyChanged
    {
        public CategoryGroup()
        {
            _bits = new ObservableCollection<DoorBitConfig>();
            _bits.CollectionChanged += Bits_CollectionChanged;
        }

        private BitCategoryModel _category;
        /// <summary>分类对象</summary>
        public BitCategoryModel Category 
        { 
            get => _category; 
            set { _category = value; OnPropertyChanged(); } 
        }

        private ObservableCollection<DoorBitConfig> _bits;
        /// <summary>该分类下的点位集合</summary>
        public ObservableCollection<DoorBitConfig> Bits 
        { 
            get => _bits; 
            set 
            {
                if (_bits != null)
                {
                    _bits.CollectionChanged -= Bits_CollectionChanged;
                    foreach (var b in _bits) b.PropertyChanged -= Bit_PropertyChanged;
                }
                _bits = value; 
                if (_bits != null)
                {
                    _bits.CollectionChanged += Bits_CollectionChanged;
                    foreach (var b in _bits) b.PropertyChanged += Bit_PropertyChanged;
                }
                OnPropertyChanged();
                UpdateCount();
            } 
        }

        /// <summary>激活的点位数量</summary>
        public int ActiveCount => Bits?.Count(b => b.BitValue) ?? 0;

        private void Bits_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (DoorBitConfig b in e.OldItems) b.PropertyChanged -= Bit_PropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (DoorBitConfig b in e.NewItems) b.PropertyChanged += Bit_PropertyChanged;
            }
            UpdateCount();
        }

        private void Bit_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DoorBitConfig.BitValue))
            {
                UpdateCount();
            }
        }

        public void UpdateCount()
        {
            OnPropertyChanged(nameof(ActiveCount));
        }
    }
}
