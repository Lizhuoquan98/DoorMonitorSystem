
using Communicationlib;
using Communicationlib.config;
using ControlLibrary.Models;
using DoorMonitorSystem.Models;
using DoorMonitorSystem.Models.Points;
using DoorMonitorSystem.Models.system;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DoorMonitorSystem
{

    public static class GlobalData
    {

        #region 门数据 
         
         
        /// <summary>
        /// 点位状态颜色集合
        /// </summary>
        public static List<BitColor> ListBitColor { get; set; } = []; 
        
        /// <summary>
        /// 事件记录等级
        /// </summary>
        public static List<RecordLevelDict> RecordLevels { get; set; } = [];
        /// <summary>
        /// 报警模式
        /// </summary>
        public static List<AlarmModeDict> AlarmModes { get; set; } = [];
        /// <summary>
        /// 布尔状态描述
        /// </summary>
        public static List<BitDescriptionDict> BitStauts { get; set; } = [];

        /// <summary>
        /// 设备点位列表
        /// </summary>
    //    public static List<DevicePoint> DevicePointList { get; set; } = [];


        /// <summary>
        /// UITag路径
        /// </summary>
        public static ObservableCollection<PathDirectory> PathTreeList { get; set; } = [];

        #endregion

        #region 系统配置文件数据
        /// <summary>
        ///  门图形控制集合
        /// </summary>
        public static Dictionary<string, List<IconItem>>? GraphicDictionary { get; set; } = [];// 图形数据字典，键为图形名称，值为图形集合 

        /// <summary>
        /// 获取设备集合
        /// </summary>
        public static List<ConfigEntity> ListDveices { get; set; } = [];
        /// <summary>
        /// 
        /// </summary>
        public static  SysCfg? SysCfg { get; set; } 
        
        /// <summary>
        /// NTP 服务配置 (NtpConfig.json)
        /// </summary>
        public static NtpConfig? NtpConfig { get; set; } 
        
        /// <summary>
        /// 调试配置 (DebugConfig.json)
        /// </summary>
        public static AppDebugConfig DebugConfig { get; set; } = new AppDebugConfig();

        /// <summary>
        /// 加载所有通讯协议插件
        /// </summary>
        public static Dictionary<string, ICommBase> ProtocolsPairs { get; set; } = [];
        #endregion
       



        /// <summary>
        /// 全局主界面视图模型引用 (用于通讯服务直接更新 UI)
        /// </summary>
        public static DoorMonitorSystem.ViewModels.MainViewModel? MainVm { get; set; }

        /// <summary>
        /// 当前登录用户
        /// </summary>
        public static UserEntity? CurrentUser { get; set; }
    }

}
