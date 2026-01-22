
using ControlLibrary.Models;
using DoorMonitorSystem.Base;
using DoorMonitorSystem.Models.ConfigEntity;
using DoorMonitorSystem.Models.ConfigEntity.Door;
using DoorMonitorSystem.Models.ConfigEntity.Group;
using DoorMonitorSystem.Models.RunModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;

namespace DoorMonitorSystem.ViewModels
{

    public class ConfigureViewModel : NotifyPropertyChanged
    {
        public ObservableCollection<StationMainGroup> Stations { get; set; } = new ObservableCollection<StationMainGroup>();

        private CancellationTokenSource _updateToken = new();


        public ConfigureViewModel()
        {

              
        } 

    } 

}