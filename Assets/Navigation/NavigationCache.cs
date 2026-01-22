
using System.Collections.Generic;
using System.Windows.Controls;


namespace DoorMonitorSystem.Assets.Navigation
{
 
    public static class NavigationCache
    {
        public static Dictionary<string, object> ViewModels { get; } = new();
        public static Dictionary<string, UserControl> Views { get; } = new();
    }

}
