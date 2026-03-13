using CommunityToolkit.Mvvm.ComponentModel;
using OCR_Facturas.Models;

namespace OCR_Facturas.ViewModels
{
    // Le decimos a MAUI que espere recibir un parámetro llamado "Invoice"
    public partial class InvoiceDetailViewModel : ObservableObject, IQueryAttributable
    {
        [ObservableProperty]
        private InvoiceDto _invoice;

        // 2. Este método se dispara automáticamente cuando Shell.Current.GoToAsync te envía parámetros
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            // Buscamos la clave "Invoice" exacta que pusiste en el diccionario
            if (query.ContainsKey("Invoice") && query["Invoice"] != null)
            {
                // Rescatamos el objeto y lo asignamos a nuestra propiedad
                Invoice = query["Invoice"] as InvoiceDto;

                // (Opcional) Limpiamos el parámetro para que no se quede en memoria
                query.Clear();
            }
        }
    }
}
