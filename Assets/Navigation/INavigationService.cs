using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace DoorMonitorSystem.Assets.Navigation
{
    public interface INavigationService
    {
        void NavigateTo<TViewModel>() where TViewModel : class, new();
        void NavigateTo<TViewModel>(object parameter) where TViewModel : class, new();

        UserControl? CurrentView { get; }
        object? CurrentViewModel { get; }
    }
}
