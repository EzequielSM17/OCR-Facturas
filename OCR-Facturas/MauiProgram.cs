using Microsoft.Extensions.Logging;
using OCR_Facturas.Models;
using OCR_Facturas.Services;
using OCR_Facturas.Services.Interface;
using OCR_Facturas.ViewModels;
using OCR_Facturas.Views;

namespace OCR_Facturas
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            var appSettings = new AppSettings {
                AzureEndpoint = "https://ocr-facturasezequiel.cognitiveservices.azure.com/",

                // La clave (puedes dejarla vacía en producción por seguridad, o poner una de dev)
                AzureKey = "",

                // Tu conexión a Supabase por defecto
                PostgresConnectionString = "Host=localhost;Database=OCR-facturas;Username=postgres;Password=prueba1234"
            };
            builder.Services.AddSingleton(appSettings);

            builder.Services.AddSingleton<IAzureOcrService, AzureOcrService>();
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

            builder.Services.AddTransient<MainViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<HistoryViewModel>();

            builder.Services.AddTransient<InvoiceDetailViewModel>();

            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<HistoryPage>();
            builder.Services.AddTransient<InvoiceDetailPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
