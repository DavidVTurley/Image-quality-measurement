using System.Windows.Input;

namespace Imcheck.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private int _selectedTabIndex;
    private string _statusText = "Ready";

    public MainWindowViewModel()
    {
        SelectQ13Command = new RelayCommand(() => SelectedTabIndex = 0);
        SelectUniformityCommand = new RelayCommand(() => SelectedTabIndex = 1);
        SelectGeneratorCommand = new RelayCommand(() => SelectedTabIndex = 2);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public ICommand SelectQ13Command { get; }

    public ICommand SelectUniformityCommand { get; }

    public ICommand SelectGeneratorCommand { get; }
}
