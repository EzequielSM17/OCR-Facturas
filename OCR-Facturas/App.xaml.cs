using Microsoft.Extensions.DependencyInjection;

namespace OCR_Facturas
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(Views.InvoiceDetailPage), typeof(Views.InvoiceDetailPage));
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}