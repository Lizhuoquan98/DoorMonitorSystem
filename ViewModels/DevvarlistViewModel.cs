using DoorMonitorSystem.Base;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Sharp7;
using DoorMonitorSystem.Models;
using Base;


namespace DoorMonitorSystem.ViewModels
{
   

    public class DevvarlistViewModel:NotifyPropertyChanged
    {
    
   //     public ObservableCollection<DevicePoint> PointList { get; set; } = new ObservableCollection<DevicePoint>(GlobalData.DevicePointList); 
     //   public DevicePoint NewPoint { get; set; } = new(); 
        public S7WordLength  DataTypes { get; set; } 
        //public List<string> RecordLevels { get; set; } = GlobalData.RecordLevels.Select(x => x.RecordLevels  ).ToList(); 
        //public List<string> AlarmModes { get; set; } =  GlobalData.AlarmModes.Select(x => x.AlarmModes  ).ToList(); 
        //public List<string> BitStauts { get; set; } = GlobalData.BitStauts.Select(x => x.BitStatus).ToList(); 

        private ObservableCollection<PathDirectory> _pathTreeList = [];

        public ObservableCollection<PathDirectory>  PathTreeList
        {
            get { return _pathTreeList  ; }
            set { _pathTreeList  = value;  OnPropertyChanged(); }
        } 

        private PathDirectory _selectedPath=new PathDirectory();

        public PathDirectory SelectedPath
        {
            get { return _selectedPath; }
            set { _selectedPath = value; OnPropertyChanged(); }
        }


        public ICommand ScanPathCommand => new RelayCommand(ScanPath);

        private void ScanPath(object obj)
        { 
            PathTreeList.Clear(); // 清空之前的路径列表 
            PathTreeList = new ObservableCollection<PathDirectory>([
                new PathDirectory
                {
                    PathName = "主界面",
                    FullPath = "主界面",
                    Paths = new List<PathDirectory>
                    {
                        new PathDirectory { PathName = "上行门", FullPath = "主界面.上行门" },
                        new PathDirectory { PathName = "上行组", FullPath = "主界面.上行组" },
                        new PathDirectory { PathName = "下行门", FullPath = "主界面.下行门" },
                        new PathDirectory { PathName = "下行组", FullPath = "主界面.下行组" }
                    }
                }
            ]);

            var root = PathTreeList[0];
            var upDoor = root.Paths[0];
            var upGroup = root.Paths[1];
            var downDoor = root.Paths[2];
            var downGroup = root.Paths[3]; 

            //// ========== 分组：DveGroup → Bit ==========
            //upGroup.Paths = GlobalData.ListDveGroup
            //                 .Where(x => x.SubType == 1)
            //                 .Select(x => new PathDirectory
            //                 {
            //                     PathName = x.Description,
            //                     FullPath = upGroup.FullPath + "." + x.Description,
            //                     Paths = x.GroupBits.Select(y => new PathDirectory
            //                     {
            //                         PathName = y.BitDescription,
            //                         FullPath = upGroup.FullPath + "." + x.Description + "." + y.BitDescription
            //                     }).ToList()
            //                 }).ToList();

            //downGroup.Paths = GlobalData.ListDveGroup
            //               .Where(x => x.SubType == 2)
            //               .Select(x => new PathDirectory
            //               {
            //                   PathName = x.Description,
            //                   FullPath = upGroup.FullPath + "." + x.Description,
            //                   Paths = x.GroupBits.Select(y => new PathDirectory
            //                   {
            //                       PathName = y.BitDescription,
            //                       FullPath = upGroup.FullPath + "." + x.Description + "." + y.BitDescription
            //                   }).ToList()
            //               }).ToList();
             
            // ========== 门：DoorSet → Bit ========== 

            //upDoor.Paths = GlobalData.DoorList.Where (d => d.Deviceid == 1).Select(d => new PathDirectory
            //{
            //    PathName = d.DeviceName,
            //    FullPath = upDoor.FullPath + "." + d.DeviceName,
            //    Paths = GlobalData.DoorBitList
            //        .Where(b => b.DoorType == d.DoorType)
            //        .Select(b => new PathDirectory
            //        {
            //            PathName = b.Description,
            //            FullPath = upDoor.FullPath + "." + d.DeviceName + "." + b.Description
            //        }).ToList()
            //}).ToList(); 

            //downDoor.Paths = GlobalData.DoorList.Where(d => d.Deviceid == 2).Select(d => new PathDirectory
            //{
            //    PathName = d.DeviceName,
            //    FullPath = upDoor.FullPath + "." + d.DeviceName,
            //    Paths = GlobalData.DoorBitList
            //     .Where(b => b.DoorType == d.DoorType)
            //     .Select(b => new PathDirectory
            //     {
            //         PathName = b.Description,
            //         FullPath = upDoor.FullPath + "." + d.DeviceName + "." + b.Description
            //     }).ToList()
            //}).ToList();
              
        }

        public ICommand AddPointCommand => new RelayCommand(AddPoint);
   

        private void AddPoint(object obj )
        {
            //NewPoint.PointId = PointList.Count+1;
            //NewPoint.UiBinding =   string.IsNullOrEmpty( SelectedPath.FullPath)  ?  SelectedPath.FullPath : "";
            //PointList.Add(NewPoint.Clone());
            //NewPoint = new DevicePoint(); // 重置输入项
            //OnPropertyChanged(nameof(NewPoint));
        }
        public DevvarlistViewModel() { 
             
            //DictIdToNameConverter.AlarmModeDict = GlobalData.AlarmModes.ToDictionary(x => x.Id, x => x.AlarmModes);
            //DictIdToNameConverter.RecordLevelDict = GlobalData.RecordLevels.ToDictionary(x => x.Id, x => x.RecordLevels );

        }
    }

 
}
