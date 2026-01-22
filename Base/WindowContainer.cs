using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Base
{
   public  class WindowContainer
    {

        private static readonly Dictionary<string, WindowInf> _windows = [];  

        public  static  void    Register<T>(string name,Window window)
        {
            if (!_windows.ContainsKey(name))
                _windows.Add(name, new WindowInf { WindowType = typeof(T), Owber = window });

        } 


        public static bool ShowDialog(string name) 
        {
            if (_windows.ContainsKey(name))
            {
                WindowInf inf = _windows[name]; 
                Type type = inf.WindowType; 
                Window window = (Window)Activator.CreateInstance(type);
                window.Owner = inf.Owber;
                return window.ShowDialog() ==true;
           
            }
            return false;   
        }

        public static bool Show(string name)
        {
            if (_windows.ContainsKey(name))
            {
                WindowInf inf = _windows[name];
                Type type = inf.WindowType;
                Window window = (Window)Activator.CreateInstance(type);
                window.Owner = inf.Owber;
                window.Show();
                return  true;
            }
            return false;
        }

        public static bool IsWindowOpen<T>(string name = "") where T : Window
        {
            return string.IsNullOrEmpty(name)
                ? Application.Current.Windows.OfType<T>().Any()
                : Application.Current.Windows.OfType<T>().Any(w => w.Name.Equals(name));
        }
    }

    internal class WindowInf
    {

        public Type WindowType { get; set; }

        public  Window Owber { get; set; }


    }
}
