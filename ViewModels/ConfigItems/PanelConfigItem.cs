using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Base;
using Base;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System;

namespace DoorMonitorSystem.ViewModels.ConfigItems
{
    public class PanelConfigItem : ConfigItemBase
    {
        public PanelEntity Entity { get; set; }
        public PanelGroupConfigItem Parent { get; set; }
        private readonly Dictionary<string, ObservableCollection<PanelBitConfigEntity>> _bitMap;
        
        private readonly LayoutConfigurationViewModel _mainVm;
        
        public PanelConfigItem(PanelEntity entity, PanelGroupConfigItem parent, Dictionary<string, ObservableCollection<PanelBitConfigEntity>> bitMap, LayoutConfigurationViewModel mainVm)
        {
            Entity = entity;
            Parent = parent;
            _bitMap = bitMap;
            _mainVm = mainVm;
            Name = entity.PanelName;

            AddBitCommand = new RelayCommand(_ => AddBit());
            RemoveBitCommand = new RelayCommand(p => RemoveBit(p as PanelBitConfigEntity));
            SaveBitConfigsCommand = new RelayCommand(_ => SaveBitConfigs());
        }

        public ObservableCollection<PanelBitConfigEntity> BitConfigs
        {
            get
            {
                if (string.IsNullOrEmpty(Entity.KeyId) || _bitMap == null) return null;
                if (!_bitMap.ContainsKey(Entity.KeyId))
                {
                    _bitMap[Entity.KeyId] = new ObservableCollection<PanelBitConfigEntity>();
                }
                return _bitMap[Entity.KeyId];
            }
        }

        private PanelBitConfigEntity _editingBit;
        public PanelBitConfigEntity EditingBit
        {
            get => _editingBit;
            set { _editingBit = value; OnPropertyChanged(); }
        }

        private bool _isBitEditPopupOpen;
        public bool IsBitEditPopupOpen
        {
            get => _isBitEditPopupOpen;
            set { _isBitEditPopupOpen = value; OnPropertyChanged(); }
        }

        public ICommand StartEditBitCommand => new RelayCommand(p => StartEditBit(p as PanelBitConfigEntity));
        public ICommand CancelEditBitCommand => new RelayCommand(_ => IsBitEditPopupOpen = false);
        public ICommand ConfirmEditBitCommand => new RelayCommand(_ => ConfirmEditBit());

        private void StartEditBit(PanelBitConfigEntity bit)
        {
            if (bit == null) return;
            EditingBit = new PanelBitConfigEntity
            {
                Id = bit.Id,
                KeyId = bit.KeyId,
                PanelKeyId = bit.PanelKeyId,
                Description = bit.Description,
                ByteOffset = bit.ByteOffset,
                BitIndex = bit.BitIndex,
                DataType = bit.DataType,
                HighColorId = bit.HighColorId,
                LowColorId = bit.LowColorId,
                LogTypeId = bit.LogTypeId,
                SortOrder = bit.SortOrder
            };
            IsBitEditPopupOpen = true;
        }

        private void ConfirmEditBit()
        {
            if (EditingBit == null) return;
            var target = BitConfigs?.FirstOrDefault(x => x.KeyId == EditingBit.KeyId);
            if (target != null)
            {
                target.Description = EditingBit.Description;
                target.ByteOffset = EditingBit.ByteOffset;
                target.BitIndex = EditingBit.BitIndex;
                target.DataType = EditingBit.DataType;
                target.HighColorId = EditingBit.HighColorId;
                target.LowColorId = EditingBit.LowColorId;
                target.LogTypeId = EditingBit.LogTypeId;
                target.SortOrder = EditingBit.SortOrder;
            }
            IsBitEditPopupOpen = false;
            SaveBitConfigs(true); // 自动保存
        }

        public ICommand AddBitCommand { get; }
        public ICommand RemoveBitCommand { get; }
        public ICommand SaveBitConfigsCommand { get; }

        public override string Name
        {
            get => base.Name;
            set
            {
                base.Name = value;
                Entity.PanelName = value;
            }
        }

        public void UpdateName() => Name = Entity.PanelName;

        // 当面板类型改变时通知 UI 刷新列表
        public void RaisePropertyChangeForPanelType()
        {
            OnPropertyChanged(nameof(Entity));
            // 类型改变不影响点表，因为点表现在是跟 Panel 绑定的
            // OnPropertyChanged(nameof(BitConfigs)); 
        }

        private void AddBit()
        {
            var configs = BitConfigs;
            if (configs == null) return;
            configs.Add(new PanelBitConfigEntity 
            { 
                PanelKeyId = Entity.KeyId, 
                KeyId = Guid.NewGuid().ToString(),
                Description = "新面板点位",
                SortOrder = configs.Count > 0 ? configs.Max(c => c.SortOrder) + 1 : 1
            });
            SaveBitConfigs(true); // 自动保存
        }

        private void RemoveBit(PanelBitConfigEntity item)
        {
            if (item == null) return;

            if (MessageBox.Show($"确定要从[面板点表]中物理删除点位 \"{item.Description}\" 吗？\n此操作将直接同步至数据库。", 
                "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();

                    // 物理删除数据库中的记录 (根据 KeyId 唯一标识)
                    db.ExecuteNonQuery($"DELETE FROM PanelBitConfig WHERE KeyId = '{item.KeyId}'");

                    // 从内存列表中移除以刷新 UI
                    BitConfigs?.Remove(item);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"物理删除点位失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveBitConfigs(object param) => SaveBitConfigs(false);

        private void SaveBitConfigs(bool silent = false)
        {
            var configs = BitConfigs;
            if (configs == null || string.IsNullOrEmpty(Entity.KeyId)) return;

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();

                // 2. 更新或插入
                foreach (var bit in configs)
                {
                    // 确保 PanelKeyId 正确
                    bit.PanelKeyId = Entity.KeyId;
                    
                    // 检查是否存在并获取 ID (根据 KeyId)
                    var existObj = db.Query<PanelBitConfigEntity>($"SELECT Id FROM PanelBitConfig WHERE KeyId = '{bit.KeyId}'").FirstOrDefault();
                    
                    if (existObj != null)
                    {
                        bit.Id = existObj.Id;
                        db.Update(bit);
                    }
                    else
                    {
                        bit.Id = 0; // 确保是插入
                        db.Insert(bit);
                    }
                }

                // 处理删除逻辑: 获取数据库中该面板的现有 KeyId 列表
                var existingKeys = db.Query<PanelBitConfigEntity>($"SELECT KeyId FROM PanelBitConfig WHERE PanelKeyId = '{Entity.KeyId}'")
                                     .Select(x => x.KeyId).ToList();
                var currentKeys = configs.Select(x => x.KeyId).ToList();

                foreach (var key in existingKeys)
                {
                    if (!currentKeys.Contains(key))
                    {
                        db.ExecuteNonQuery($"DELETE FROM PanelBitConfig WHERE KeyId = '{key}'");
                    }
                }

                if (!silent)
                {
                    MessageBox.Show($"面板点位配置已成功保存到数据库 (面板 ID: {Entity.KeyId})", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    if (_mainVm != null) _mainVm.StatusMessage = "面板点位已自动同步至数据库";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存面板点位配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
