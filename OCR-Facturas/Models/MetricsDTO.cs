using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas.Models
{
    public class InvoiceOcrMetricsDto
    {
        public decimal DocumentConfidence { get; set; }      // confianza global Azure
        public decimal AverageConfidence { get; set; }       // media calculada

        public InvoiceFieldConfidenceDto MerchantName { get; set; } = new();
        public InvoiceFieldConfidenceDto IssueDate { get; set; } = new();
        public InvoiceFieldConfidenceDto TotalAmount { get; set; } = new();
        public InvoiceFieldConfidenceDto Items { get; set; } = new();

        public List<InvoiceItemOcrMetricsDto> ItemMetrics { get; set; } = new();
    }
    public class InvoiceFieldConfidenceDto
    {
        public bool Found { get; set; }
        public decimal Confidence { get; set; }   // 0..100
        public bool Inferred { get; set; }        // si lo calculaste tú
    }
    public class InvoiceItemOcrMetricsDto
    {
        public int Index { get; set; }
        public decimal AverageConfidence { get; set; }

        public InvoiceFieldConfidenceDto Description { get; set; } = new();
        public InvoiceFieldConfidenceDto Quantity { get; set; } = new();
        public InvoiceFieldConfidenceDto UnitPrice { get; set; } = new();
        public InvoiceFieldConfidenceDto TotalPrice { get; set; } = new();
        public InvoiceFieldConfidenceDto Tax { get; set; } = new();
    }
    public class InvoiceExtractionResultDto
    {
        public InvoiceDto Invoice { get; set; } = new();
        public InvoiceOcrMetricsDto OcrMetrics { get; set; } = new();
    }
    public class InvoiceItemDisplayDto
    {
        public string Description { get; set; }
        public double Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public int IvaAmount { get; set; }

        public decimal AverageConfidence { get; set; }
        public decimal DescriptionConfidence { get; set; }
        public decimal QuantityConfidence { get; set; }
        public decimal UnitPriceConfidence { get; set; }
        public decimal TotalPriceConfidence { get; set; }
        public decimal TaxConfidence { get; set; }

        public bool QuantityInferred { get; set; }

        public string AverageConfidenceText => $"{AverageConfidence:0.##}%";
        public string DescriptionConfidenceText => $"{DescriptionConfidence:0.##}%";
        public string QuantityConfidenceText => QuantityInferred ? "Inferido" : $"{QuantityConfidence:0.##}%";
        public string UnitPriceConfidenceText => $"{UnitPriceConfidence:0.##}%";
        public string TotalPriceConfidenceText => $"{TotalPriceConfidence:0.##}%";
        public string TaxConfidenceText => $"{TaxConfidence:0.##}%";

        public Color ConfidenceColor => GetConfidenceColor(AverageConfidence);

        private static Color GetConfidenceColor(decimal confidence)
        {
            if (confidence >= 80)
                return Color.FromArgb("#10B981"); // verde

            if (confidence >= 50)
                return Color.FromArgb("#FACC15"); // amarillo

            return Color.FromArgb("#EF4444"); // rojo
        }
    }
}
