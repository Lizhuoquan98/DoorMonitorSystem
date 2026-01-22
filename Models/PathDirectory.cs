using DoorMonitorSystem.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks; 

namespace DoorMonitorSystem.Models
{

    public class PathDirectory : NotifyPropertyChanged
    { 

        private string _pathName = "";

        public string PathName
        {
            get { return _pathName; }
            set { _pathName = value; OnPropertyChanged(); }
        }


        private string _fullPath = "";

       public string FullPath
        {
            get { return _fullPath; }
            set { _fullPath = value; OnPropertyChanged(); }
        }



        private List<PathDirectory>? _paths;

        public List<PathDirectory>? Paths
        {
            get { return _paths; }
            set { _paths = value; OnPropertyChanged(); }
        }


        public override string ToString() => FullPath; // ComboBox.SelectedItem 显示用
        }
     
}
