using WinAudioRouter.App.ViewModels;
using WinAudioRouter.Core.Audio.Services;

namespace WinAudioRouter.App;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private Animation? _pulseAnimation;
    private bool _animationRunning;

    public MainPage(MainViewModel viewModel, IAudioDeviceManager audioDeviceManager)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDevicesCommand.ExecuteAsync(null);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRouting))
        {
            if (_viewModel.IsRouting)
                StartPulseAnimation();
            else
                StopPulseAnimation();
        }
    }

    private void StartPulseAnimation()
    {
        if (_animationRunning) return;

        var indicator = RoutingIndicator;
        if (indicator is null) return;

        _pulseAnimation = new Animation(v => indicator.Opacity = v, 1, 0.3);
        _pulseAnimation.Commit(this, "PulseAnimation", length: 1000, easing: Easing.SinInOut,
            repeat: () => _animationRunning);
        _animationRunning = true;
    }

    private void StopPulseAnimation()
    {
        _animationRunning = false;
        this.AbortAnimation("PulseAnimation");

        var indicator = RoutingIndicator;
        if (indicator is not null)
            indicator.Opacity = 1;
    }
}
