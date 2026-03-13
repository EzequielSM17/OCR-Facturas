using OCR_Facturas.ViewModels;

namespace OCR_Facturas.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Ejecutamos el comando de carga automáticamente al entrar en la pantalla
        if (!_viewModel.IsLoading)
        {
            _viewModel.LoadHistoryCommand.Execute(null);
        }
    }
}