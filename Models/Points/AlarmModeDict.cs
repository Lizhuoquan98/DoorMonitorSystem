using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoorMonitorSystem.Models.Points
{
    /// <summary>
    /// 报警模式字典
    /// </summary>
    public class AlarmModeDict
    {

        public int Id { get; set; }
         
        public string AlarmModes { get; set; } = ""; //报警模式 
       
    }
}
