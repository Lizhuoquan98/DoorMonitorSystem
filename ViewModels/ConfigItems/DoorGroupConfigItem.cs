using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Base;
using Base;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System;

namespace DoorMonitorSystem.ViewModels.ConfigItems
{
    public class DoorGroupConfigItem : ConfigItemBase
    {
        public DoorGroupEntity Entity { get; set; }
        public StationConfigItem Parent { get; set; }
        private readonly Dictionary<string, ObservableCollection<DoorBitConfigEntity>> _bitMap;
        private readonly LayoutConfigurationViewModel _mainVm;

        public DoorGroupConfigItem(DoorGroupEntity entity, StationConfigItem parent, Dictionary<string, ObservableCollection<DoorBitConfigEntity>> bitMap, LayoutConfigurationViewModel mainVm)
        {
            Entity = entity;
            Parent = parent;
            _bitMap = bitMap;
            _mainVm = mainVm;

            // 如果 Entity 名字为空或者默认值，尝试使用标准命名
            if (string.IsNullOrEmpty(entity.GroupName) || entity.GroupName == "新门组")
                Name = parent.Name + "门组";
            else
                Name = entity.GroupName;

            AddBitCommand = new RelayCommand(_ => AddBit());
            RemoveBitCommand = new RelayCommand(p => RemoveBit(p as DoorBitConfigEntity));
            SaveBitConfigsCommand = new RelayCommand(_ => SaveBitConfigs());
        }

        public string SelectedDoorTypeKeyId
        {
            get => _mainVm.GlobalSelectedDoorTypeKeyId;
            set 
            { 
                if (_mainVm.GlobalSelectedDoorTypeKeyId != value)
                {
                    _mainVm.GlobalSelectedDoorTypeKeyId = value;
                    OnPropertyChanged(); 
                    OnPropertyChanged(nameof(BitConfigs)); 
                }
            }
        }

        public ObservableCollection<DoorBitConfigEntity> BitConfigs
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedDoorTypeKeyId) || _bitMap == null) return null;
                if (!_bitMap.ContainsKey(SelectedDoorTypeKeyId))
                {
                    _bitMap[SelectedDoorTypeKeyId] = new ObservableCollection<DoorBitConfigEntity>();
                }
                return _bitMap[SelectedDoorTypeKeyId];
            }
        }

        private DoorBitConfigEntity _editingBit;
        public DoorBitConfigEntity EditingBit
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

        public ICommand StartEditBitCommand => new RelayCommand(p => StartEditBit(p as DoorBitConfigEntity));
        public ICommand CancelEditBitCommand => new RelayCommand(_ => IsBitEditPopupOpen = false);
        public ICommand ConfirmEditBitCommand => new RelayCommand(_ => ConfirmEditBit());

        private void StartEditBit(DoorBitConfigEntity bit)
        {
            if (bit == null) return;
            // 创建副本用于编辑，防止取消时也同步了修改
            EditingBit = new DoorBitConfigEntity
            {
                Id = bit.Id,
                KeyId = bit.KeyId,
                DoorTypeKeyId = bit.DoorTypeKeyId,
                Description = bit.Description,
                ByteOffset = bit.ByteOffset,
                BitIndex = bit.BitIndex,
                DataType = bit.DataType,
                CategoryId = bit.CategoryId,
                HeaderPriority = bit.HeaderPriority,
                ImagePriority = bit.ImagePriority,
                BottomPriority = bit.BottomPriority,
                HighColorId = bit.HighColorId,
                LowColorId = bit.LowColorId,
                HeaderColorId = bit.HeaderColorId,
                BottomColorId = bit.BottomColorId,
                GraphicName = bit.GraphicName,
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
                // 将编辑后的值同步回列表中的实体
                target.Description = EditingBit.Description;
                target.ByteOffset = EditingBit.ByteOffset;
                target.BitIndex = EditingBit.BitIndex;
                target.DataType = EditingBit.DataType;
                target.CategoryId = EditingBit.CategoryId;
                target.HeaderPriority = EditingBit.HeaderPriority;
                target.ImagePriority = EditingBit.ImagePriority;
                target.BottomPriority = EditingBit.BottomPriority;
                target.HighColorId = EditingBit.HighColorId;
                target.LowColorId = EditingBit.LowColorId;
                target.HeaderColorId = EditingBit.HeaderColorId;
                target.BottomColorId = EditingBit.BottomColorId;
                target.GraphicName = EditingBit.GraphicName;
                target.LogTypeId = EditingBit.LogTypeId;
                target.SortOrder = EditingBit.SortOrder;

                // 触发 UI 刷新 (由于可能实体没实现 NotifyPropertyChanged 针对所有字段，或者 DataGrid 需要通知)
                // DoorBitConfigEntity 看起来可能没有全字段通知，但 DataGrid 绑定通常能捕捉到
            }
            IsBitEditPopupOpen = false;
            SaveBitConfigs(true); // 自动保存到数据库
        }

        public ICommand AddBitCommand { get; }
        public ICommand RemoveBitCommand { get; }
        public ICommand SaveBitConfigsCommand { get; }

        public void RaisePropertyChangeForDoorType()
        {
            OnPropertyChanged(nameof(SelectedDoorTypeKeyId));
            OnPropertyChanged(nameof(BitConfigs));
        }

        private void AddBit()
        {
            var configs = BitConfigs;
            if (configs == null) return;
            configs.Add(new DoorBitConfigEntity 
            { 
                DoorTypeKeyId = SelectedDoorTypeKeyId, 
                KeyId = Guid.NewGuid().ToString(),
                Description = "新点位",
                SortOrder = configs.Count > 0 ? configs.Max(c => c.SortOrder) + 1 : 1
            });
            SaveBitConfigs(true); // 自动同步新增点位
        }

        private void RemoveBit(DoorBitConfigEntity item)
        {
            if (item == null) return;

            if (MessageBox.Show($"确定要从[门点表]中物理删除点位 \"{item.Description}\" 吗？\n此操作将直接同步至数据库，且可能影响所有同类型的门。", 
                "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                    db.Connect();

                    // 物理删除数据库中的记录 (根据 KeyId 唯一标识)
                    db.ExecuteNonQuery($"DELETE FROM DoorBitConfig WHERE KeyId = '{item.KeyId}'");

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
            if (configs == null || string.IsNullOrEmpty(SelectedDoorTypeKeyId)) return;

            try
            {
                using var db = new SQLHelper(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
                db.Connect();

                // 为了简单起见，这里采用先删除该类型所有点位再重新插入的方法，或者逐个 Update/Insert
                // 鉴于已经有 KeyId，逐个 Save 更稳妥 (SQLHelper.Save 可能是 InsertOrUpdate)
                
                // 首先获取数据库中该类型的现有 KeyId 列表，用于处理删除逻辑
                var existingKeys = db.Query<DoorBitConfigEntity>($"SELECT KeyId FROM DoorBitConfig WHERE DoorTypeKeyId = '{SelectedDoorTypeKeyId}'")
                                     .Select(x => x.KeyId).ToList();
                
                var currentKeys = configs.Select(x => x.KeyId).ToList();

                // 1. 删除已在界面移除但在数据库存在的
                foreach (var key in existingKeys)
                {
                    if (!currentKeys.Contains(key))
                    {
                        db.ExecuteNonQuery($"DELETE FROM DoorBitConfig WHERE KeyId = '{key}'");
                    }
                }

                // 2. 更新或插入
                foreach (var bit in configs)
                {
                    // 确保 DoorTypeKeyId 正确
                    bit.DoorTypeKeyId = SelectedDoorTypeKeyId;
                    
                    // 检查是否存在并获取 ID (根据 KeyId)
                    // 注意：必须获取 ID 赋值给实体，否则 Update(Where Id=0) 会失败
                    var existObj = db.Query<DoorBitConfigEntity>($"SELECT Id FROM DoorBitConfig WHERE KeyId = '{bit.KeyId}'").FirstOrDefault();
                    
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

                if (!silent)
                {
                    MessageBox.Show($"点位配置已成功保存到数据库 (类型 ID: {SelectedDoorTypeKeyId})", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // 如果有 MainVM，可以更新状态栏
                    if (_mainVm != null) _mainVm.StatusMessage = "点位已自动同步至数据库";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析或保存点位配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public override string Name
        {
            get => base.Name;
            set
            {
                base.Name = value;
                Entity.GroupName = value;
            }
        }

        public void UpdateName() => Name = Entity.GroupName;
    }
}
