using Breb.App.ViewModels;

namespace Breb.App.Views;

public partial class TransferenciaPage : ContentPage
{
    private readonly TransferenciaViewModel _viewModel;

    public TransferenciaPage(TransferenciaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AlAparecerAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.DetenerSondeo();
    }
}
