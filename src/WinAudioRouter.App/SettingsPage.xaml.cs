using WinAudioRouter.App.ViewModels;

namespace WinAudioRouter.App;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (BindingContext is SettingsViewModel vm)
        {
            await vm.LoadSettingsCommand.ExecuteAsync(null);
        }
    }
}
