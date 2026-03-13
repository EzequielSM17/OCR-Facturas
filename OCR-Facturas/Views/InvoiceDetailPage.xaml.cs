
using OCR_Facturas.ViewModels;

namespace OCR_Facturas.Views;

public partial class InvoiceDetailPage : ContentPage
{
    private readonly InvoiceDetailViewModel _viewModel;
    public InvoiceDetailPage(InvoiceDetailViewModel viewModel)

    {
		InitializeComponent();
        BindingContext = viewModel;
    }
}