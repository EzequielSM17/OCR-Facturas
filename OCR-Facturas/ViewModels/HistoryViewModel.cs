using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace OCR_Facturas.ViewModels
{
    public class InvoiceGroup : List<InvoiceDto>
    {
        public string MonthYear { get; private set; }
        public decimal GroupTotal { get; private set; }

        public InvoiceGroup(string monthYear, IEnumerable<InvoiceDto> invoices) : base(invoices)
        {
            MonthYear = monthYear;
            // Calculamos el total de este mes automáticamente
            GroupTotal = invoices.Sum(i => i.TotalAmount);
        }
    }

    public partial class HistoryViewModel : ObservableObject
    {
        private readonly IDatabaseService _dbService;

        // Colección agrupada para el UI
        [ObservableProperty]
        private ObservableCollection<InvoiceGroup> _groupedInvoices = new();

        [ObservableProperty]
        private decimal _totalYearly; // Total del año actual, por ejemplo

        [ObservableProperty]
        private bool _isLoading;

        public HistoryViewModel(IDatabaseService dbService)
        {
            _dbService = dbService;
        }

        // Se llama cada vez que se entra a la pantalla de historial
        [RelayCommand]
        public async Task LoadHistoryAsync()
        {
            IsLoading = true;
            try
            {
                var invoices = await _dbService.GetAllInvoicesAsync();

                // 1. Calcular el total del año actual
                int currentYear = DateTime.Now.Year;
                TotalYearly = invoices.Where(i => i.IssueDate.Year == currentYear)
                                      .Sum(i => i.TotalAmount);

                // 2. Agrupar por Año y Mes
                var query = invoices
                    .OrderByDescending(i => i.IssueDate)
                    .GroupBy(i => new { i.IssueDate.Year, i.IssueDate.Month })
                    .Select(g =>
                    {
                        var dateName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy");
                        dateName = char.ToUpper(dateName[0]) + dateName.Substring(1);
                        return new InvoiceGroup(dateName, g);
                    })
                    // Es vital materializar la query aquí con ToList() antes de ir al MainThread
                    .ToList();

                // 3. Actualizar la colección de la UI (¡Blindado para Windows!)
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var newCollection = new ObservableCollection<InvoiceGroup>();
                    foreach (var group in query)
                    {
                        newCollection.Add(group);
                    }

                    // Reemplazamos la lista completa en lugar de hacer .Clear()
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

            // Pasamos el objeto entero de la factura a la nueva pantalla
            var parametros = new Dictionary<string, object>
    {
        { "Invoice", facturaSeleccionada }
    };

            // Navegamos a la pantalla de detalle
            await Shell.Current.GoToAsync(nameof(Views.InvoiceDetailPage), parametros);
        }
    }
}
