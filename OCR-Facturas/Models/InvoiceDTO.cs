using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas.Models
{
    public class InvoiceDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime IssueDate { get; set; }
        public string MerchantName { get; set; }
        public decimal TotalAmount { get; set; }
        
        public string LocalImagePath { get; set; } = null!;

        // Relación con los productos
        public List<InvoiceItemDto> Items { get; set; } = new();
    }

    // Models/InvoiceItemDto.cs
    public class InvoiceItemDto
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid InvoiceId { get; set; } // Clave foránea
        public string Description { get; set; }
        public double Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal IvaAmount { get; set; }

        public decimal TotalPrice { get; set; }
    }
}
