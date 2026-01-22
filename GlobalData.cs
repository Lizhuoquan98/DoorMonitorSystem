
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
        /// 所有门集合
        /// </summary>
      //  public static List<DoorSet> DoorList { get; set; } = [];

        /// <summary>
        /// 门点位显示集合
        /// </summary>
     //   public static List<DoorBitConfig> DoorBitList { get; set; } = [];

        /// <summary>
        /// 颜色集合
        /// </summary>
  //      public static List<Listcolor> ListColors { get; set; } = [];

        /// <summary>
        /// 点位状态颜色集合
        /// </summary>
        public static List<BitColor> ListBitColor { get; set; } = [];

        /// <summary>
        /// 主界面分组设备集合
        /// </summary>
   //     public static List<DveGroup> ListDveGroup { get; set; } = [];

        /// <summary>
        /// 主界面分组设备点位集合
        /// </summary>
    //    public static List<GroupBit> ListGroupBit { get; set; } = [];

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
        /// 加载所有通讯协议插件
        /// </summary>
        public static Dictionary<string, ICommBase> ProtocolsPairs { get; set; } = [];
        #endregion
       



    }

}
