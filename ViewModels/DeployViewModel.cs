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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DoorMonitorSystem.ViewModels
{
    /// <summary>
    /// 用于配置设备信息的视图模型，支持加载/保存设备列表、增删设备和参数。
    /// </summary>
    public class DeployViewModel : NotifyPropertyChanged
    {
        #region 字段和属性

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
                OnPropertyChanged(nameof(IsDeviceSelected));
            }
        }

        /// <summary>
        /// 是否有选中的设备（用于UI绑定）
        /// </summary>
        public bool IsDeviceSelected => SelectedDevice != null && SelectedDeviceIndex >= 0;

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
                    OnPropertyChanged(nameof(IsDeviceSelected));
                }
            }
        }

        private int _selectedProtocolIndex = -1;
        public int SelectedProtocolIndex
        {
            get => _selectedProtocolIndex;
            set
            {
                if (_selectedProtocolIndex != value)
                {
                    _selectedProtocolIndex = value;
                    OnPropertyChanged();

                    // 当协议变更时，更新参数列表
                    if (_selectedProtocolIndex >= 0 && SelectedDevice != null)
                    {
                        UpdateDeviceParameters();
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
        /// 连接测试状态消息
        /// </summary>
        private string _testStatusMessage = "";
        public string TestStatusMessage
        {
            get => _testStatusMessage;
            set
            {
                _testStatusMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否正在测试连接
        /// </summary>
        private bool _isTesting = false;
        public bool IsTesting
        {
            get => _isTesting;
            set
            {
                _isTesting = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region 命令

        /// <summary>
        /// 添加设备命令
        /// </summary>
        public ICommand AddDeviceCommand => new RelayCommand(_ =>
        {
            var newDevice = new ConfigEntity
            {
                Name = $"新设备 {Devices.Count + 1}",
                ID = Devices.Count + 1,
                Protocol = ProtocolKeys.FirstOrDefault() ?? "",
                CommParsams = []
            };

            Devices.Add(newDevice);
            SelectedDeviceIndex = Devices.Count - 1;
            SelectedDevice = newDevice;
        });

        /// <summary>
        /// 删除设备命令
        /// </summary>
        public ICommand DeleteDeviceCommand => new RelayCommand(_ =>
        {
            if (SelectedDeviceIndex < 0 || SelectedDeviceIndex >= Devices.Count)
            {
                MessageBox.Show("请先选择要删除的设备！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除设备 \"{SelectedDevice?.Name}\" 吗？",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                Devices.RemoveAt(SelectedDeviceIndex);
                SelectedDevice = Devices.FirstOrDefault();
                SelectedDeviceIndex = Devices.Any() ? 0 : -1;
            }
        });

        /// <summary>
        /// 保存命令，将 Devices 集合保存为 JSON 文件
        /// </summary>
        public ICommand SaveCommand => new RelayCommand(SaveToJson);

        /// <summary>
        /// 测试连接命令
        /// </summary>
        public ICommand TestConnectionCommand => new RelayCommand(async _ => await TestConnection());

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数，自动加载配置文件
        /// </summary>
        public DeployViewModel()
        {
            LoadFromJson();
            LoadProtocolKeys();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载协议列表
        /// </summary>
        private void LoadProtocolKeys()
        {
            ProtocolKeys.Clear();

            // TODO: 从 GlobalData.ProtocolsPairs 加载协议列表
            // 暂时添加示例协议
            ProtocolKeys.Add("S7-1200");
            ProtocolKeys.Add("S7-1500");
            ProtocolKeys.Add("Modbus TCP");
            ProtocolKeys.Add("OPC UA");
        }

        /// <summary>
        /// 更新设备参数列表（根据协议特性生成）
        /// </summary>
        private void UpdateDeviceParameters()
        {
            if (SelectedDevice == null || _selectedProtocolIndex < 0) return;

            // TODO: 根据协议类型，使用反射获取特性标记的参数
            // var protocolType = GlobalData.ProtocolsPairs[ProtocolKeys[_selectedProtocolIndex]].GetType();
            // SelectedDevice.CommParsams = new ObservableCollection<CommParamEntity>(GetProtocolConfig(protocolType));
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
                        if (Devices.Any())
                        {
                            SelectedDeviceIndex = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载配置文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 将设备配置保存为 JSON 文件
        /// </summary>
        private void SaveToJson(object obj)
        {
            try
            {
                if (SelectedDevice == null || SelectedDeviceIndex < 0 || SelectedDeviceIndex >= Devices.Count)
                {
                    // 直接保存整个列表
                    ConvertDataToJsoncs.SaveDataToJson(Devices, "Config/devices.json");
                    MessageBox.Show("配置保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 更新当前选中的设备
                Devices[SelectedDeviceIndex] = SelectedDevice;
                ConvertDataToJsoncs.SaveDataToJson(Devices, "Config/devices.json");
                MessageBox.Show("配置保存成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 测试设备连接
        /// </summary>
        private async Task TestConnection()
        {
            if (SelectedDevice == null)
            {
                TestStatusMessage = "❌ 请先选择要测试的设备！";
                return;
            }

            IsTesting = true;
            TestStatusMessage = "🔄 正在测试连接...";

            try
            {
                // 模拟连接测试（延迟1秒）
                await Task.Delay(1000);

                // TODO: 实际的连接测试逻辑
                // 根据协议类型创建通信对象并测试连接
                // var comm = GlobalData.ProtocolsPairs[SelectedDevice.Protocol];
                // bool isConnected = await comm.TestConnection(SelectedDevice.CommParsams);

                // 模拟测试结果
                bool isConnected = new Random().Next(0, 2) == 1;

                if (isConnected)
                {
                    TestStatusMessage = $"✅ 设备 \"{SelectedDevice.Name}\" 连接成功！";
                }
                else
                {
                    TestStatusMessage = $"❌ 设备 \"{SelectedDevice.Name}\" 连接失败！";
                }
            }
            catch (Exception ex)
            {
                TestStatusMessage = $"❌ 连接测试异常: {ex.Message}";
            }
            finally
            {
                IsTesting = false;
            }
        }

        #endregion

        #region 静态方法

        /// <summary>
        /// 根据协议类型获取参数配置（通过反射读取特性）
        /// </summary>
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
                    Value = "" // 默认值为空
                });
            }
            return list;
        }

        #endregion
    }
}
