using OCR_Facturas.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas.Services.Interface
{
    public interface IDatabaseService
    {
        Task SaveInvoiceAsync(InvoiceDto invoice);
        Task<IEnumerable<InvoiceDto>> GetAllInvoicesAsync();
    }
}
