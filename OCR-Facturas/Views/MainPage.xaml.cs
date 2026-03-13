using OCR_Facturas.Models;
using OCR_Facturas.ViewModels;

namespace OCR_Facturas.Views;

public partial class MainPage : ContentPage
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = viewModel;
    }
    // AÑADE ESTE MÉTODO AQUÍ
    
}