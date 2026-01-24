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

            // 注意：协议插件的加载和启动已移至 DeviceCommunicationService.StartAsync 中统一管理
            // 这里仅保留基本数据的加载
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
            mysql.CreateTableFromModel<BitCategoryEntity>();
            mysql.CreateTableFromModel<DevicePointConfigEntity>();

            // 加载设备点位配置到全局缓存 (将在 CommunicationService 中处理)
            // GlobalData.ListDevicePoints = mysql.FindAll<DevicePointConfigEntity>();
            
        }
        /// <summary>
        /// 从单字节读取指定位
        /// </summary>
        public static bool GetBitValue(byte data, int bitOffset)
        {
            return ((data >> bitOffset) & 1) == 1;
        }
    }
}
