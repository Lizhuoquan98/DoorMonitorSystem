using Communicationlib.config;
using ControlLibrary.Models;
using DoorMonitorSystem.Assets.Commlib;
using DoorMonitorSystem.Assets.Helper;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using DoorMonitorSystem.Models.system;
using DoorMonitorSystem.ViewModels;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
namespace DoorMonitorSystem.Assets.Database
{
    public class LoadDefaultData
    {

        public LoadDefaultData()
        {
            // 初始化全局数据
            ImportGraphicDictionary();
            //加载配置文件 SysCfg

            LoadFromJson();
            // 从数据库加载数据
            GetSqlData();

            //{
            //    //var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Communication.Protocol.dll");
            //    //var comm = ProtocolLoader.LoadAllProtocols(dllPath);
            //    // 获取插件所在文件夹（假设 Communication.Protocol.dll 就在这个目录下）
            //    var pluginFolder = AppDomain.CurrentDomain.BaseDirectory;

            //    // 加载所有插件
            //    GlobalData.ProtocolsPairs = ProtocolLoader.LoadAllProtocols(pluginFolder);
            //    if (GlobalData.ProtocolsPairs is null)
            //        return;
            //    var protocols = GlobalData.ProtocolsPairs;

            //    foreach (var dev in GlobalData.ListDveices)
            //    {
            //        if (!protocols.TryGetValue(dev.Protocol, out var protocol))
            //        {
            //            Debug.WriteLine($"设备 {dev.Name} 找不到协议 {dev.Protocol}");
            //            continue;
            //        }
            //        protocol.Initialize(dev.CommParsams);
            //        try
            //        {
            //            protocol.Open();
            //            if (!protocol.IsConnected)
            //                continue;
            //            Debug.WriteLine($"{dev.Name} 协议 {dev.Protocol} 打开成功");
            //            // 启动一个独立任务，专门执行协议方法调度
            //            _ = Task.Run(async () =>
            //            {
            //                var methods = protocol.GetSupportedMethods();
            //                // 每个方法各自执行
            //                var tasks = new List<Task>();

            //                // Startup 模式执行一次
            //                foreach (var m in methods.Values.Where(m => m.Mode == ExecutionMode.Startup))
            //                {
            //                    _ = Task.Run(async () =>
            //                    {
            //                        try { await m.InvokeAsync(); }
            //                        catch (Exception ex) { Debug.WriteLine($"Startup {m.Name} error: {ex.Message}"); }
            //                    });
            //                }
            //                foreach (var pair in methods)
            //                {
            //                    var method = pair.Value;
            //                    if (method.Mode == ExecutionMode.Polling)
            //                    {
            //                        tasks.Add(Task.Run(async () =>
            //                        {
            //                            while (protocol.IsConnected && method.Enabled)
            //                            {
            //                                try
            //                                {
            //                                    var req = (addr: (ushort)1, count: (ushort)5);
            //                                    var result = await method.InvokeAsync(req);
            //                                    method.IntervalMs = 100;
            //                                    if (result is ushort[] words)
            //                                    {
            //                                        // 调用业务分发
            //                                        //  GlobalData.UpdateDeviceValues(dev.ID, words);
            //                                    }

            //                                }
            //                                catch (Exception ex)
            //                                {
            //                                    Debug.WriteLine($"[{dev.Name}][{method.Name}] 出错: {ex.Message}");
            //                                }
            //                                await Task.Delay(method.IntervalMs);
            //                            }
            //                        }));
            //                    } 
            //                }
            //                await Task.WhenAll(tasks);
            //            }); 
            //        }
            //        catch (Exception ex)
            //        {

            //            Debug.WriteLine($"{dev.Name} 协议 {dev.Protocol} 通讯建立失败！！");
            //            Debug.WriteLine($"{ex.ToString}");
            //        }


            //    }
            //}

        }

        /// <summary>
        /// 加载滑动门图标数据字典
        /// </summary>
        static void ImportGraphicDictionary()
        {
            string path = "GraphicDictionary.json"; // 或指定绝对路径

            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var imported = JsonSerializer.Deserialize<List<SerializableGraphicGroup>>(json);
                    if (imported != null)
                    {
                        GlobalData.GraphicDictionary?.Clear();

                        foreach (var group in imported)
                        {
                            var itemList = new List<IconItem>();
                            foreach (var item in group.Items)
                            {
                                itemList.Add(new IconItem
                                {
                                    Data = Geometry.Parse(item.PathData),
                                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.StrokeColor)),
                                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(item.FillColor)),
                                    StrokeThickness = item.StrokeThickness,
                                    IsSelected = false
                                });
                            }

                            GlobalData.GraphicDictionary[group.Name] = itemList;

                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"加载图标失败：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 从 JSON 文件加载设备配置
        /// </summary>
        static void LoadFromJson()
        {
            try
            {
                String path = "Config/devices.json";
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<List<ConfigEntity>>(json);
                    if (list != null)
                    {
                        GlobalData.ListDveices = list;
                    }
                }
                path = "Config/SystemConfig.json";
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var Response = JsonSerializer.Deserialize<SysCfg>(json);
                    if (Response != null)
                    {
                        GlobalData.SysCfg = Response;
                    }
                }
            }
            catch (Exception ex)
            {

                MessageBox.Show($"Error loading {ex}");
            }

        }
   
        /// <summary>
        /// 从数据库中读取数据并填充到全局数据模型中
        /// </summary>
        static void GetSqlData()
        {
            using SQLHelper mysql = new(GlobalData.SysCfg.ServerAddress, GlobalData.SysCfg.UserName, GlobalData.SysCfg.UserPassword, GlobalData.SysCfg.DatabaseName);
            mysql.Connect();

            // 获取数据库信息
            Debug.WriteLine($"数据库版本: {mysql.GetDatabaseVersion()}");
            Debug.WriteLine($"数据库大小: {mysql.GetDatabaseSize()} MB");

            // 创建所有表
            mysql.CreateTableFromModel<BitColorEntity>();
            mysql.CreateTableFromModel<StationEntity>();
            mysql.CreateTableFromModel<DoorTypeEntity>();
            mysql.CreateTableFromModel<DoorBitConfigEntity>();
            mysql.CreateTableFromModel<DoorGroupEntity>();
            mysql.CreateTableFromModel<DoorEntity>();
            mysql.CreateTableFromModel<PanelGroupEntity>();
            mysql.CreateTableFromModel<PanelEntity>();
            mysql.CreateTableFromModel<PanelBitConfigEntity>();

            // 检查是否需要插入初始数据（如果站台表为空，则插入模拟数据）
            var existingStations = mysql.SelectAll<StationEntity>();
            if (existingStations.Count == 0)
            {
                Debug.WriteLine("开始插入模拟数据...");
             //   InsertMockData(mysql);
                Debug.WriteLine("模拟数据插入完成！");
            }
            else
            {
                Debug.WriteLine($"数据库已有 {existingStations.Count} 个站台，跳过插入模拟数据");
            }

           
             
        }

 

        /// <summary>
        /// 从单个字节中获取指定位的值
        /// </summary>
        /// <param name="data">字节数据</param>
        /// <param name="bitOffset">位偏移（0-7，0是最低位，7是最高位）</param>
        /// <returns>位值（true=1, false=0）</returns>
        public static bool GetBitValue(byte data, int bitOffset)
        {
            if (bitOffset < 0 || bitOffset > 7)
                throw new ArgumentOutOfRangeException(nameof(bitOffset),
                    $"位偏移 {bitOffset} 超出范围 [0, 7]");

            // 将指定位右移到最低位，然后与1进行与操作
            return ((data >> bitOffset) & 1) == 1;
        }



    }
}
