using OCR_Facturas.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas.Services.Interface
{
    public interface IAzureOcrService
    {
        Task<InvoiceDto> ExtractInvoiceDataAsync(FileStream imageStream);
    }
}
