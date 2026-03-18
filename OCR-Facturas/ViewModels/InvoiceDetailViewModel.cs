using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCR_Facturas.ViewModels
{
    public partial class InvoiceDetailViewModel : ObservableObject, IQueryAttributable
    {
        private readonly IDatabaseService _dbService;

        [ObservableProperty]
        private decimal _totalFacturaVisual;

        [ObservableProperty]
        private InvoiceDto _invoice;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isReadOnly = true;

        public InvoiceDetailViewModel(IDatabaseService dbService)
        {
            _dbService = dbService;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.ContainsKey("Invoice") && query["Invoice"] != null)
            {
                // Desuscribimos la factura anterior si existía
                DesuscribirEventosItems(Invoice);

                Invoice = query["Invoice"] as InvoiceDto;
                query.Clear();

                // Suscribimos la nueva factura
                SuscribirEventosItems(Invoice);

                IsEditing = false;
                IsReadOnly = true;

                RecalcularTotales();
            }
        }

        partial void OnInvoiceChanged(InvoiceDto value)
        {
            DesuscribirEventosItems(Invoice);
            SuscribirEventosItems(value);
            RecalcularTotales();
        }

        [RelayCommand]
        public void ToggleEdit()
        {
            IsEditing = !IsEditing;
            IsReadOnly = !IsEditing;
        }

        [RelayCommand]
        public async Task GuardarCambiosAsync()
        {
            if (Invoice == null) return;

            try
            {
                RecalcularTotales();

                await _dbService.UpdateInvoiceAsync(Invoice);

                IsEditing = false;
                IsReadOnly = true;

                await Shell.Current.DisplayAlert("Éxito", "La factura se ha actualizado correctamente.", "OK");
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"No se pudieron guardar los cambios: {ex.Message}", "OK");
            }
        }

        [RelayCommand]
        public void RecalcularTotales()
        {
            if (Invoice?.Items == null)
                return;

            decimal nuevoTotal = 0;

            foreach (var item in Invoice.Items)
            {
                item.TotalPrice = (decimal)item.Quantity * item.UnitPrice;
                nuevoTotal += item.TotalPrice;
            }

            Invoice.TotalAmount = nuevoTotal;
            TotalFacturaVisual = nuevoTotal;

            // Refresca bindings que dependen del objeto completo
            OnPropertyChanged(nameof(Invoice));
            OnPropertyChanged(nameof(TotalFacturaVisual));
        }

        private void SuscribirEventosItems(InvoiceDto factura)
        {
            if (factura?.Items == null)
                return;

            foreach (var item in factura.Items)
            {
                if (item is INotifyPropertyChanged observableItem)
                {
                    observableItem.PropertyChanged += Item_PropertyChanged;
                }
            }

            if (factura.Items is INotifyCollectionChanged observableCollection)
            {
                observableCollection.CollectionChanged += Items_CollectionChanged;
            }
        }

        private void DesuscribirEventosItems(InvoiceDto factura)
        {
            if (factura?.Items == null)
                return;

            foreach (var item in factura.Items)
            {
                if (item is INotifyPropertyChanged observableItem)
                {
                    observableItem.PropertyChanged -= Item_PropertyChanged;
                }
            }

            if (factura.Items is INotifyCollectionChanged observableCollection)
            {
                observableCollection.CollectionChanged -= Items_CollectionChanged;
            }
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InvoiceItemDto.Quantity) ||
                e.PropertyName == nameof(InvoiceItemDto.UnitPrice))
            {
                RecalcularTotales();
            }
        }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is INotifyPropertyChanged observableItem)
                    {
                        observableItem.PropertyChanged -= Item_PropertyChanged;
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is INotifyPropertyChanged observableItem)
                    {
                        observableItem.PropertyChanged += Item_PropertyChanged;
                    }
                }
            }

            RecalcularTotales();
        }
    }
}