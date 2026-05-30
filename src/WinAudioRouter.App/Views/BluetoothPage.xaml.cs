using WinAudioRouter.App.ViewModels;

namespace WinAudioRouter.App.Views;

public partial class BluetoothPage : ContentPage
{
    private readonly BluetoothViewModel _viewModel;

    public BluetoothPage(BluetoothViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.DiscoveredDevices.Count == 0)
        {
            await _viewModel.ScanDevicesCommand.ExecuteAsync(null);
        }
    }
}
