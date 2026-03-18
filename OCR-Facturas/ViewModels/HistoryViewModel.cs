using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCR_Facturas.ViewModels
{
    public class InvoiceGroup : List<InvoiceDto>
    {
        public string MonthYear { get; private set; }
        public decimal GroupTotal { get; private set; }

        public InvoiceGroup(string monthYear, IEnumerable<InvoiceDto> invoices) : base(invoices)
        {
            MonthYear = monthYear;
            GroupTotal = invoices.Sum(i => i.TotalAmount);
        }
    }

    public partial class HistoryViewModel : ObservableObject
    {
        private readonly IDatabaseService _dbService;

        [ObservableProperty]
        private ObservableCollection<InvoiceGroup> _groupedInvoices = new();

        [ObservableProperty]
        private decimal _totalYearly;

        [ObservableProperty]
        private bool _isLoading;

        public HistoryViewModel(IDatabaseService dbService)
        {
            _dbService = dbService;
        }

        [RelayCommand]
        public async Task LoadHistoryAsync()
        {
            IsLoading = true;
            try
            {
                var invoices = await _dbService.GetAllInvoicesAsync();

                int currentYear = DateTime.Now.Year;
                TotalYearly = invoices.Where(i => i.IssueDate.Year == currentYear)
                                      .Sum(i => i.TotalAmount);

                var query = invoices
                    .OrderByDescending(i => i.IssueDate)
                    .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
                    .Select(g =>
                    {
                        var dateName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy");
                        dateName = char.ToUpper(dateName[0]) + dateName.Substring(1);
                        return new InvoiceGroup(dateName, g);
                    })
                    .ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var newCollection = new ObservableCollection<InvoiceGroup>();
                    foreach (var group in query)
                    {
                        newCollection.Add(group);
                    }
                    GroupedInvoices = newCollection;
                });
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"No se pudo cargar el historial: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task VerDetalleAsync(InvoiceDto facturaSeleccionada)
        {
            if (facturaSeleccionada == null) return;

            var parametros = new Dictionary<string, object>
            {
                { "Invoice", facturaSeleccionada }
            };

            await Shell.Current.GoToAsync(nameof(Views.InvoiceDetailPage), parametros);
        }

        // --- NUEVO COMANDO: ELIMINAR FACTURA ---
        [RelayCommand]
        public async Task EliminarFacturaAsync(InvoiceDto facturaSeleccionada)
        {
            if (facturaSeleccionada == null) return;

            // 1. Mostrar Modal de Confirmación
            bool confirmacion = await Shell.Current.DisplayAlert(
                "Eliminar Factura",
                $"¿Estás seguro de que deseas eliminar la factura de '{facturaSeleccionada.MerchantName}' por {facturaSeleccionada.TotalAmount:C}?",
                "Sí, eliminar",
                "Cancelar");

            // 2. Si el usuario dice que sí, borramos y recargamos
            if (confirmacion)
            {
                try
                {
                    IsLoading = true;

                    // Borrar de la base de datos
                    await _dbService.DeleteInvoiceAsync(facturaSeleccionada);

                    // Recargar la lista visual para reflejar el cambio
                    await LoadHistoryAsync();

                    await Shell.Current.DisplayAlert("Éxito", "Factura eliminada correctamente.", "OK");
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("Error", $"Fallo al eliminar: {ex.Message}", "OK");
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }
    }
}