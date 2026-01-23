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
                
                // 重置测试状态信息
                TestStatusMessage = string.Empty;

                // 同步协议索引
                if (_selectedDevice != null && !string.IsNullOrEmpty(_selectedDevice.Protocol))
                {
                    var index = ProtocolKeys.IndexOf(_selectedDevice.Protocol);
                    if (index >= 0)
                    {
                        _selectedProtocolIndex = index;
                        OnPropertyChanged(nameof(SelectedProtocolIndex));
                    }
                }
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

                    // 切换设备索引时清空测试状态
                    TestStatusMessage = string.Empty;
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

                    // 切换协议时清空测试状态
                    TestStatusMessage = string.Empty;

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
            // 自动生成唯一 ID：现有最大 ID + 1
            int nextId = Devices.Any() ? Devices.Max(d => d.ID) + 1 : 1;

            var newDevice = new ConfigEntity
            {
                Name = $"新设备 {Devices.Count + 1}",
                ID = nextId,
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

        private void LoadProtocolKeys()
        {
            ProtocolKeys.Clear();

            if (GlobalData.ProtocolsPairs != null)
            {
                foreach (var key in GlobalData.ProtocolsPairs.Keys)
                {
                    ProtocolKeys.Add(key);
                }
            }
            
            // 如果为空，保持默认加载（降级处理）
            if (ProtocolKeys.Count == 0)
            {
                ProtocolKeys.Add("S7-1200");
                ProtocolKeys.Add("S7-1500");
                ProtocolKeys.Add("Modbus TCP");
                ProtocolKeys.Add("Modbus RTU");
            }
        }

        private void UpdateDeviceParameters()
        {
            if (SelectedDevice == null || _selectedProtocolIndex < 0) return;

            string protocolKey = ProtocolKeys[_selectedProtocolIndex];
            SelectedDevice.Protocol = protocolKey;

            if (GlobalData.ProtocolsPairs != null && GlobalData.ProtocolsPairs.TryGetValue(protocolKey, out var protocol))
            {
                var protocolType = protocol.GetType();
                var newParams = GetProtocolConfig(protocolType);
                
                // 保留已有参数的值（如果名称匹配）
                foreach (var p in newParams)
                {
                    var existing = SelectedDevice.CommParsams.FirstOrDefault(x => x.Name == p.Name);
                    if (existing != null)
                    {
                        p.Value = existing.Value;
                    }
                }
                
                SelectedDevice.CommParsams = newParams.ToList();
            }
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
                if (GlobalData.ProtocolsPairs == null || !GlobalData.ProtocolsPairs.TryGetValue(SelectedDevice.Protocol, out var protocolProto))
                {
                    TestStatusMessage = $"❌ 未找到协议 \"{SelectedDevice.Protocol}\" 的实现！";
                    return;
                }

                // 通过反射创建测试实例，避免干扰运行中的服务
                var testComm = Activator.CreateInstance(protocolProto.GetType()) as Communicationlib.ICommBase;
                if (testComm == null)
                {
                    TestStatusMessage = "❌ 无法创建协议测试实例！";
                    return;
                }

                TestStatusMessage = $"🔄 正在初始化 \"{SelectedDevice.Protocol}\" 并尝试连接...";
                
                bool isConnected = await Task.Run(() =>
                {
                    try
                    {
                        testComm.Initialize(SelectedDevice.CommParsams.ToList());
                        testComm.Open();
                        bool connected = testComm.IsConnected;
                        testComm.Close();
                        return connected;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                });

                if (isConnected)
                {
                    TestStatusMessage = $"✅ 设备 \"{SelectedDevice.Name}\" ({SelectedDevice.Protocol}) 连接成功！";
                }
                else
                {
                    TestStatusMessage = $"❌ 设备 \"{SelectedDevice.Name}\" ({SelectedDevice.Protocol}) 连接失败，请检查参数或网络环境。";
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
