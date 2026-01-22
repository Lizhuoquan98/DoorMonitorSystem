using Base;
using Communicationlib.Abstract;
using Communicationlib.config;
using DoorMonitorSystem.Assets.ConvertData;
using DoorMonitorSystem.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 用于配置设备信息的视图模型，支持加载/保存设备列表、增删设备和参数。
    /// </summary>
    public class DeployViewModel : NotifyPropertyChanged
    {
        /// <summary>
        /// 所有设备配置集合
        /// </summary>
        private ObservableCollection<ConfigEntity> _devices = [];


        public ObservableCollection<ConfigEntity> Devices
        {
            get { return _devices; }
            set
            {
                _devices = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> ProtocolKeys { get; set; } = [];

        //private string? _selectedProtocolKey;
        //public string? SelectedProtocolKey
        //{
        //    get => _selectedProtocolKey;
        //    set
        //    {
        //        _selectedProtocolKey = value;
        //        OnPropertyChanged( );
        //        // 可以在这里触发 protocol 变更逻辑
        //    }
        //}
        private ConfigEntity? _selectedDevice = new ConfigEntity();

        /// <summary>
        /// 当前选中的设备
        /// </summary>
        public ConfigEntity? SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                _selectedDevice = value?.Clone();
                OnPropertyChanged();
            }
        }

        private int _selectedDeviceIndex = -1;
        public int SelectedDeviceIndex
        {
            get => _selectedDeviceIndex;
            set
            {
                if (_selectedDeviceIndex != value)
                {
                    _selectedDeviceIndex = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool addDevice=false;

        private int _selectedProtocolIndex = -1;
        public int SelectedProtocolIndex
        {
            get => _selectedProtocolIndex;
            set
            {
                if (_selectedProtocolIndex != value)
                {
                    _selectedProtocolIndex = value;
                    if (_selectedProtocolIndex>=0 && addDevice)
                    {
                        addDevice = false;
                        //SelectedDevice.CommParsams= GetProtocolConfig( GlobalData.ProtocolsPairs[ProtocolKeys[_selectedProtocolIndex]].GetType());
                   
                    }
                   
                }
            }
        }


        private int _selectedParaIndex = -1;
        public int SelectedparaIndex
        {
            get => _selectedParaIndex;
            set
            {
                if (_selectedParaIndex != value)
                {
                    _selectedParaIndex = value;

                    OnPropertyChanged();
                }
            }
        }
        /// <summary>
        /// 添加设备命令
        /// </summary>
        public ICommand AddDeviceCommand => new RelayCommand(_ =>
        {
            
            SelectedDevice = new ConfigEntity();
            Devices.Add(SelectedDevice);

        });

        /// <summary>
        /// 添加参数命令（针对当前选中的设备）
        /// </summary>
        public ICommand AddParamCommand => new RelayCommand(_ =>
        {
            if (Devices == null || SelectedDeviceIndex == -1) return;
            Devices[SelectedDeviceIndex].CommParsams.Add(new CommParamEntity
            {
                Name = "New Parameter",
                Value = "Default Value"
            });
            SelectedDevice = Devices[SelectedDeviceIndex].Clone();
        });



        /// <summary>
        /// 保存命令，将 Devices 集合保存为 JSON 文件
        /// </summary>
        public ICommand SaveCommand => new RelayCommand(SaveToJson);

        /// <summary>
        /// 构造函数，自动加载配置文件
        /// </summary>
        public DeployViewModel()
        {
            LoadFromJson();
            ProtocolKeys.Clear();
            //foreach (var key in GlobalData.ProtocolsPairs.Keys)
            //{
            //    ProtocolKeys.Add(key);
            //}
        }

        public static List<CommParamEntity> GetProtocolConfig(Type pluginType)
        {
            

            // 扫描所有实例属性（public + non-public），如果你只想公开属性改为 BindingFlags.Public | Instance
            var props = pluginType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var list = new List<CommParamEntity>();

            foreach (var prop in props)
            {
                // 查找我们定义的特性（注意命名空间）
                var attr = prop.GetCustomAttribute<ProtocolConfigAttribute>();
                if (attr == null) continue;

                list.Add(new CommParamEntity
                { 
                    Name = attr.DisplayName,
                });
            }
            return list;
        }




        /// <summary>
        /// 从 JSON 文件加载设备配置
        /// </summary>
        private void LoadFromJson()
        {
            try
            {
                if (File.Exists("Config/devices.json"))
                {
                    var json = File.ReadAllText("Config/devices.json");
                    var list = JsonSerializer.Deserialize<List<ConfigEntity>>(json);
                    if (list != null)
                    {
                        Devices = new ObservableCollection<ConfigEntity>(list);
                        SelectedDevice = Devices.FirstOrDefault();

                    }
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }

        }

        /// <summary>
        /// 将设备配置保存为 JSON 文件
        /// </summary>
        /// <param name="obj">未使用，可为 null</param>
        private void SaveToJson(object obj)
        {
            if (SelectedDevice == null || SelectedDeviceIndex < 0 || SelectedDeviceIndex >= Devices.Count)
            {
                return; // 无效的设备索引或设备列表
            }
            Devices[SelectedDeviceIndex] = SelectedDevice;
            addDevice = true;
            ConvertDataToJsoncs.SaveDataToJson(Devices, "Config/devices.json");
        }
    }
}
