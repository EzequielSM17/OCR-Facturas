using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OCR_Facturas.ViewModels
{
    
    public partial class MainViewModel : ObservableObject
    {
        private readonly IAzureOcrService _ocrService;
        private readonly IDatabaseService _dbService;
        private string _localFilePath;

        [ObservableProperty]
        private ImageSource _previewImage;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private string _statusMessage = "Listo para escanear";

        [ObservableProperty]
        private InvoiceDto _facturaActual;

        [ObservableProperty]
        private InvoiceOcrMetricsDto _ocrMetricsActual;

        [ObservableProperty]
        private bool _mostrarResultados;

        [ObservableProperty]
        private ObservableCollection<InvoiceItemDisplayDto> _itemsDisplay = new();

        public MainViewModel(IAzureOcrService ocrService, IDatabaseService dbService)
        {
            _ocrService = ocrService;
            _dbService = dbService;
        }

        public string OcrAverageConfidenceText => FormatPercent(OcrMetricsActual?.AverageConfidence);
        public string OcrDocumentConfidenceText => FormatPercent(OcrMetricsActual?.DocumentConfidence);
        public string MerchantNameConfidenceText => FormatPercent(OcrMetricsActual?.MerchantName?.Confidence);
        public string IssueDateConfidenceText => FormatPercent(OcrMetricsActual?.IssueDate?.Confidence);
        public string TotalAmountConfidenceText => FormatPercent(OcrMetricsActual?.TotalAmount?.Confidence);
        public string ItemsConfidenceText => FormatPercent(OcrMetricsActual?.Items?.Confidence);

        public Color OcrAverageConfidenceColor => GetConfidenceColor(OcrMetricsActual?.AverageConfidence ?? 0);
        public Color OcrDocumentConfidenceColor => GetConfidenceColor(OcrMetricsActual?.DocumentConfidence ?? 0);
        public Color MerchantNameConfidenceColor => GetConfidenceColor(OcrMetricsActual?.MerchantName?.Confidence ?? 0);
        public Color IssueDateConfidenceColor => GetConfidenceColor(OcrMetricsActual?.IssueDate?.Confidence ?? 0);
        public Color TotalAmountConfidenceColor => GetConfidenceColor(OcrMetricsActual?.TotalAmount?.Confidence ?? 0);
        public Color ItemsConfidenceColor => GetConfidenceColor(OcrMetricsActual?.Items?.Confidence ?? 0);

        [RelayCommand]
        public async Task PickPhotoAsync()
        {
            try
            {
                var photo = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Selecciona la foto de tu factura",
                    FileTypes = FilePickerFileType.Images
                });

                await LoadPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Error", $"Fallo al abrir la galería: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task TakePhotoAsync()
        {
            try
            {
                if (!MediaPicker.Default.IsCaptureSupported)
                {
                    await ShowAlertAsync("Aviso", "No se detecta cámara.");
                    return;
                }

                var photo = await MediaPicker.Default.CapturePhotoAsync();
                await LoadPhotoAsync(photo);
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Error de Cámara", $"Fallo: {ex.Message}");
            }
        }

        private async Task LoadPhotoAsync(FileResult photo)
        {
            if (photo == null) return;

            try
            {
                MostrarResultados = false;
                FacturaActual = null;
                OcrMetricsActual = null;
                ItemsDisplay.Clear();

                RaiseConfidenceProperties();

                var tempDirectory = FileSystem.CacheDirectory;
                _localFilePath = Path.Combine(tempDirectory, photo.FileName);

                using (var sourceStream = await photo.OpenReadAsync())
                using (var localFileStream = File.OpenWrite(_localFilePath))
                {
                    await sourceStream.CopyToAsync(localFileStream);
                }

                PreviewImage = ImageSource.FromFile(_localFilePath);
                StatusMessage = "Imagen cargada. Lista para extraer datos.";
            }
            catch (Exception ex)
            {
                await ShowAlertAsync("Error de Archivo", $"No se pudo leer la foto: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ExtractDataAsync()
        {
            if (string.IsNullOrEmpty(_localFilePath) || !File.Exists(_localFilePath))
            {
                await ShowAlertAsync("Aviso", "Primero selecciona o toma una foto.");
                return;
            }

            IsProcessing = true;
            StatusMessage = "Extrayendo datos con IA...";
            MostrarResultados = false;

            try
            {
                using var stream = File.OpenRead(_localFilePath);

                var extractionResult = await _ocrService.ExtractInvoiceDataAsync(stream);

                FacturaActual = extractionResult.Invoice;
                OcrMetricsActual = extractionResult.OcrMetrics;

                BuildItemsDisplay();
                RaiseConfidenceProperties();

                MostrarResultados = true;
                StatusMessage = "Datos extraídos. Por favor, revisa y confirma.";
            }
            catch (Exception ex)
            {
                var mensajeReal = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                await ShowAlertAsync("Error OCR", $"Fallo en la lectura: {mensajeReal}");
                StatusMessage = "Error al extraer datos.";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        [RelayCommand]
        public async Task GuardarFacturaAsync()
        {
            if (FacturaActual == null) return;

            IsProcessing = true;
            StatusMessage = "Guardando en Supabase...";

            try
            {
                await _dbService.SaveInvoiceAsync(FacturaActual);

                await ShowAlertAsync("¡Éxito!", $"Factura guardada. Total: {FacturaActual.TotalAmount:C}");

                PreviewImage = null;
                MostrarResultados = false;
                FacturaActual = null;
                OcrMetricsActual = null;
                ItemsDisplay.Clear();

                RaiseConfidenceProperties();

                if (File.Exists(_localFilePath))
                {
                    File.Delete(_localFilePath);
                }

                _localFilePath = null;
                StatusMessage = "Listo para escanear";
            }
            catch (Exception ex)
            {
                var mensajeReal = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                await ShowAlertAsync("Error de Base de Datos", $"Motivo exacto: {mensajeReal}");
                StatusMessage = "Error al guardar en BD.";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void BuildItemsDisplay()
        {
            ItemsDisplay.Clear();

            if (FacturaActual?.Items == null || OcrMetricsActual?.ItemMetrics == null)
                return;

            var count = Math.Min(FacturaActual.Items.Count, OcrMetricsActual.ItemMetrics.Count);

            for (int i = 0; i < count; i++)
            {
                var item = FacturaActual.Items[i];
                var metric = OcrMetricsActual.ItemMetrics[i];

                ItemsDisplay.Add(new InvoiceItemDisplayDto
                {
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice,
                    IvaAmount = (int)item.IvaAmount,

                    AverageConfidence = metric.AverageConfidence,
                    DescriptionConfidence = metric.Description?.Confidence ?? 0,
                    QuantityConfidence = metric.Quantity?.Confidence ?? 0,
                    UnitPriceConfidence = metric.UnitPrice?.Confidence ?? 0,
                    TotalPriceConfidence = metric.TotalPrice?.Confidence ?? 0,
                    TaxConfidence = metric.Tax?.Confidence ?? 0,

                    QuantityInferred = metric.Quantity?.Inferred ?? false
                });
            }
        }

        private void RaiseConfidenceProperties()
        {
            OnPropertyChanged(nameof(OcrAverageConfidenceText));
            OnPropertyChanged(nameof(OcrDocumentConfidenceText));
            OnPropertyChanged(nameof(MerchantNameConfidenceText));
            OnPropertyChanged(nameof(IssueDateConfidenceText));
            OnPropertyChanged(nameof(TotalAmountConfidenceText));
            OnPropertyChanged(nameof(ItemsConfidenceText));

            OnPropertyChanged(nameof(OcrAverageConfidenceColor));
            OnPropertyChanged(nameof(OcrDocumentConfidenceColor));
            OnPropertyChanged(nameof(MerchantNameConfidenceColor));
            OnPropertyChanged(nameof(IssueDateConfidenceColor));
            OnPropertyChanged(nameof(TotalAmountConfidenceColor));
            OnPropertyChanged(nameof(ItemsConfidenceColor));
        }

        private string FormatPercent(decimal? value)
        {
            return value.HasValue ? $"{value.Value:0.##}%" : "-- %";
        }

        private Color GetConfidenceColor(decimal confidence)
        {
            if (confidence >= 80)
                return Color.FromArgb("#10B981");

            if (confidence >= 50)
                return Color.FromArgb("#FACC15");

            return Color.FromArgb("#EF4444");
        }

        private Task ShowAlertAsync(string title, string message)
        {
            return MainThread.InvokeOnMainThreadAsync(() =>
            {
                return Application.Current?.MainPage?.DisplayAlert(title, message, "OK") ?? Task.CompletedTask;
            });
        }
    }
}