using DoorMonitorSystem.Base;
using System; 
using System.Windows.Controls;

namespace DoorMonitorSystem.Assets.Navigation
{

    public class NavigationService : NotifyPropertyChanged, INavigationService
    {

     
        public UserControl? CurrentView { get; private set; }
        public object? CurrentViewModel { get; private set; }

        public void NavigateTo<TViewModel>() where TViewModel : class, new()
            => NavigateTo<TViewModel>(null!);

        public void NavigateTo<TViewModel>(object parameter) where TViewModel : class, new()
        {
            var viewModel = new TViewModel();
            if (viewModel is INavigationAware nav && parameter != null)
                nav.OnNavigatedTo(parameter);

            var viewTypeName = typeof(TViewModel).FullName!.Replace("ViewModel", "View");
            var viewType = Type.GetType(viewTypeName);
            var view = (UserControl?)Activator.CreateInstance(viewType!);
            if (view == null)
                throw new Exception($"无法创建视图类型 {viewTypeName}");

            view.DataContext = viewModel;

            CurrentViewModel = viewModel;
            CurrentView = view;
            OnPropertyChanged(nameof(CurrentView));
            OnPropertyChanged(nameof(CurrentViewModel));
        }

    }

}
