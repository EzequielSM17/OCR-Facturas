using Azure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using OCR_Facturas.Models; // Añadido para que reconozca InvoiceDto
using OCR_Facturas.Services.Interface;
using System;
using System.Collections.Generic;
using System.IO;
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

        // --- NUEVAS PROPIEDADES PARA LA UI MODERNA ---
        [ObservableProperty]
        private InvoiceDto _facturaActual;

        [ObservableProperty]
        private bool _mostrarResultados;

        public MainViewModel(IAzureOcrService ocrService, IDatabaseService dbService)
        {
            _ocrService = ocrService;
            _dbService = dbService;
        }

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
                // Limpiamos resultados anteriores al cargar foto nueva
                MostrarResultados = false;
                FacturaActual = null;

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

        // PASO 1: EXTRAER DATOS (No guarda en DB)
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
            MostrarResultados = false; // Ocultamos la tarjeta mientras carga

            try
            {
                using var stream = File.OpenRead(_localFilePath);

                // Solo leemos, no guardamos todavía
                var invoiceData = await _ocrService.ExtractInvoiceDataAsync(stream);

                // Mostramos los datos en la tarjeta
                FacturaActual = invoiceData;
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

        // PASO 2: GUARDAR EN BASE DE DATOS
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

                // Limpiamos la UI y la caché para la siguiente factura
                PreviewImage = null;
                MostrarResultados = false;
                FacturaActual = null;

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

        private Task ShowAlertAsync(string title, string message)
        {
            return MainThread.InvokeOnMainThreadAsync(() =>
            {
                return Application.Current?.MainPage?.DisplayAlert(title, message, "OK") ?? Task.CompletedTask;
            });
        }
    }
}