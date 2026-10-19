using Breb.App.ViewModels;

namespace Breb.App.Views;

public partial class SaldoPage : ContentPage
{
    private readonly SaldoViewModel _viewModel;

    public SaldoPage(SaldoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.AlAparecerAsync();
    }
}
