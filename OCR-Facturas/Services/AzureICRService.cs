using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OCR_Facturas.Services
{
    public class AzureOcrService : IAzureOcrService
    {
        private readonly AppSettings _settings;

        public AzureOcrService(AppSettings settings)
        {
            _settings = settings;
        }

        private decimal ToPercent(float confidence)
        {
            return Math.Round((decimal)confidence * 100m, 2);
        }

        private decimal AverageOrZero(IEnumerable<decimal> values)
        {
            var list = values?.Where(x => x > 0).ToList() ?? new List<decimal>();
            return list.Count == 0 ? 0 : Math.Round(list.Average(), 2);
        }

        private InvoiceFieldConfidenceDto CreateFieldConfidence(DocumentField field, bool inferred = false)
        {
            if (field == null)
            {
                return new InvoiceFieldConfidenceDto
                {
                    Found = false,
                    Confidence = 0,
                    Inferred = inferred
                };
            }

            return new InvoiceFieldConfidenceDto
            {
                Found = true,
                Confidence = ToPercent((float)field.Confidence),
                Inferred = inferred
            };
        }

        private InvoiceFieldConfidenceDto CreateInferredFieldConfidence()
        {
            return new InvoiceFieldConfidenceDto
            {
                Found = true,
                Confidence = 0,
                Inferred = true
            };
        }

        private decimal? GetDecimalFromField(DocumentField field)
        {
            if (field == null)
                return null;

            try
            {
                return field.FieldType switch
                {
                    DocumentFieldType.Double => (decimal)field.Value.AsDouble(),
                    DocumentFieldType.Int64 => field.Value.AsInt64(),
                    DocumentFieldType.Currency => (decimal)field.Value.AsCurrency().Amount,
                    DocumentFieldType.String => TryParseDecimal(field.Value.AsString()),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private string GetStringFromField(DocumentField field)
        {
            if (field == null)
                return null;

            try
            {
                return field.FieldType switch
                {
                    DocumentFieldType.String => field.Value.AsString(),
                    DocumentFieldType.Double => field.Value.AsDouble().ToString(CultureInfo.InvariantCulture),
                    DocumentFieldType.Int64 => field.Value.AsInt64().ToString(CultureInfo.InvariantCulture),
                    DocumentFieldType.Currency => field.Value.AsCurrency().Amount.ToString(CultureInfo.InvariantCulture),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private decimal? TryParseDecimal(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Trim();

            if (decimal.TryParse(input, NumberStyles.Any, new CultureInfo("es-ES"), out var valueEs))
                return valueEs;

            if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueInv))
                return valueInv;

            var cleaned = Regex.Replace(input, @"[^\d,.\-]", "");

            if (decimal.TryParse(cleaned, NumberStyles.Any, new CultureInfo("es-ES"), out valueEs))
                return valueEs;

            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out valueInv))
                return valueInv;

            return null;
        }

        private decimal? ExtraerCantidadDesdeTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            var match = Regex.Match(
                texto,
                @"(?<!\d)(\d+[.,]?\d*)\s*(uds?|unid(?:ades)?|kg|g|l|ml|h|hr|hrs|hora(?:s)?|u)?\b",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            return TryParseDecimal(match.Groups[1].Value);
        }

        private int? ExtraerPrimerNumeroEntero(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return null;

            var match = Regex.Match(texto, @"\d+");
            if (!match.Success)
                return null;

            return int.TryParse(match.Value, out var value) ? value : null;
        }

        public async Task<InvoiceExtractionResultDto> ExtractInvoiceDataAsync(FileStream imageStream)
        {
            var resultDto = new InvoiceExtractionResultDto();
            var invoiceDto = resultDto.Invoice;
            var metricsDto = resultDto.OcrMetrics;

            try
            {
                var credential = new AzureKeyCredential(_settings.AzureKey);
                var client = new DocumentAnalysisClient(new Uri(_settings.AzureEndpoint), credential);

                AnalyzeDocumentOperation operation =
                    await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-invoice", imageStream);

                AnalyzeResult result = operation.Value;

                if (result.Documents.Count > 0)
                {
                    AnalyzedDocument document = result.Documents[0];

                    metricsDto.DocumentConfidence = ToPercent(document.Confidence);

                    invoiceDto.LocalImagePath = Path.GetFileName(imageStream?.Name);

                    var globalConfidences = new List<decimal>();

                    // Fecha
                    if (document.Fields.TryGetValue("InvoiceDate", out DocumentField dateField) &&
                        dateField.FieldType == DocumentFieldType.Date)
                    {
                        var extractedDate = dateField.Value.AsDate().DateTime;
                        invoiceDto.IssueDate = DateTime.SpecifyKind(extractedDate, DateTimeKind.Utc);

                        metricsDto.IssueDate = CreateFieldConfidence(dateField);
                        globalConfidences.Add(metricsDto.IssueDate.Confidence);
                    }
                    else
                    {
                        invoiceDto.IssueDate = DateTime.UtcNow;
                        metricsDto.IssueDate = new InvoiceFieldConfidenceDto { Found = false };
                    }

                    // Total factura
                    if (document.Fields.TryGetValue("InvoiceTotal", out DocumentField totalField))
                    {
                        if (totalField.FieldType == DocumentFieldType.Double)
                            invoiceDto.TotalAmount = (decimal)totalField.Value.AsDouble();
                        else if (totalField.FieldType == DocumentFieldType.Currency)
                            invoiceDto.TotalAmount = (decimal)totalField.Value.AsCurrency().Amount;

                        metricsDto.TotalAmount = CreateFieldConfidence(totalField);
                        globalConfidences.Add(metricsDto.TotalAmount.Confidence);
                    }
                    else
                    {
                        metricsDto.TotalAmount = new InvoiceFieldConfidenceDto { Found = false };
                    }

                    // Proveedor
                    if (document.Fields.TryGetValue("VendorName", out DocumentField vendorName) &&
                        vendorName.FieldType == DocumentFieldType.String)
                    {
                        invoiceDto.MerchantName = vendorName.Value.AsString();

                        metricsDto.MerchantName = CreateFieldConfidence(vendorName);
                        globalConfidences.Add(metricsDto.MerchantName.Confidence);
                    }
                    else
                    {
                        metricsDto.MerchantName = new InvoiceFieldConfidenceDto { Found = false };
                    }

                    // Items
                    var itemAverageConfidences = new List<decimal>();

                    if (document.Fields.TryGetValue("Items", out DocumentField itemsField) &&
                        itemsField.FieldType == DocumentFieldType.List)
                    {
                        foreach (DocumentField itemField in itemsField.Value.AsList())
                        {
                            if (itemField.FieldType != DocumentFieldType.Dictionary)
                                continue;

                            var itemDict = itemField.Value.AsDictionary();
                            var invoiceItem = new InvoiceItemDto();

                            var itemMetrics = new InvoiceItemOcrMetricsDto
                            {
                                Index = invoiceDto.Items.Count
                            };

                            var itemConfidences = new List<decimal>();

                            decimal? amount = null;
                            decimal? unitPrice = null;
                            decimal? quantity = null;

                            // Descripción
                            if (itemDict.TryGetValue("Description", out DocumentField descField) &&
                                descField.FieldType == DocumentFieldType.String)
                            {
                                invoiceItem.Description = descField.Value.AsString();
                                itemMetrics.Description = CreateFieldConfidence(descField);
                                itemConfidences.Add(itemMetrics.Description.Confidence);
                            }
                            else
                            {
                                itemMetrics.Description = new InvoiceFieldConfidenceDto { Found = false };
                            }

                            // Cantidad
                            if (itemDict.TryGetValue("Quantity", out DocumentField quantityField))
                            {
                                quantity = GetDecimalFromField(quantityField);
                                itemMetrics.Quantity = CreateFieldConfidence(quantityField);
                                itemConfidences.Add(itemMetrics.Quantity.Confidence);
                            }
                            else
                            {
                                itemMetrics.Quantity = new InvoiceFieldConfidenceDto { Found = false };
                            }

                            // Precio unitario
                            if (itemDict.TryGetValue("UnitPrice", out DocumentField unitPriceField))
                            {
                                unitPrice = GetDecimalFromField(unitPriceField);
                                itemMetrics.UnitPrice = CreateFieldConfidence(unitPriceField);
                                itemConfidences.Add(itemMetrics.UnitPrice.Confidence);
                            }
                            else
                            {
                                itemMetrics.UnitPrice = new InvoiceFieldConfidenceDto { Found = false };
                            }

                            // Total línea
                            if (itemDict.TryGetValue("Amount", out DocumentField amountField))
                            {
                                amount = GetDecimalFromField(amountField);
                                itemMetrics.TotalPrice = CreateFieldConfidence(amountField);
                                itemConfidences.Add(itemMetrics.TotalPrice.Confidence);
                            }
                            else
                            {
                                itemMetrics.TotalPrice = new InvoiceFieldConfidenceDto { Found = false };
                            }

                            // Impuesto
                            if (itemDict.TryGetValue("Tax", out DocumentField taxField))
                            {
                                var taxValue = GetStringFromField(taxField);
                                if (!string.IsNullOrWhiteSpace(taxValue))
                                {
                                    var taxNumber = ExtraerPrimerNumeroEntero(taxValue);
                                    if (taxNumber.HasValue)
                                        invoiceItem.IvaAmount = taxNumber.Value;
                                }

                                itemMetrics.Tax = CreateFieldConfidence(taxField);
                                itemConfidences.Add(itemMetrics.Tax.Confidence);
                            }
                            else
                            {
                                itemMetrics.Tax = new InvoiceFieldConfidenceDto { Found = false };
                            }

                            // Cantidad con fallback
                            if (quantity.HasValue && quantity.Value > 0)
                            {
                                invoiceItem.Quantity = (double)quantity.Value;
                            }
                            else if (amount.HasValue && unitPrice.HasValue && unitPrice.Value > 0)
                            {
                                invoiceItem.Quantity = (double)decimal.Round(amount.Value / unitPrice.Value, 3);
                                itemMetrics.Quantity = CreateInferredFieldConfidence();
                            }
                            else
                            {
                                var qtyFromDescription = ExtraerCantidadDesdeTexto(invoiceItem.Description);
                                if (qtyFromDescription.HasValue)
                                {
                                    invoiceItem.Quantity = (double)qtyFromDescription.Value;
                                    itemMetrics.Quantity = CreateInferredFieldConfidence();
                                }
                            }

                            if (unitPrice.HasValue)
                                invoiceItem.UnitPrice = unitPrice.Value;

                            if (amount.HasValue)
                                invoiceItem.TotalPrice = amount.Value;

                            if (invoiceItem.TotalPrice <= 0 &&
                                invoiceItem.Quantity > 0 &&
                                invoiceItem.UnitPrice > 0)
                            {
                                invoiceItem.TotalPrice = (decimal)invoiceItem.Quantity * invoiceItem.UnitPrice;
                            }

                            itemMetrics.AverageConfidence = AverageOrZero(itemConfidences);

                            if (!string.IsNullOrWhiteSpace(invoiceItem.Description) || invoiceItem.TotalPrice > 0)
                            {
                                invoiceDto.Items.Add(invoiceItem);
                                metricsDto.ItemMetrics.Add(itemMetrics);

                                if (itemMetrics.AverageConfidence > 0)
                                    itemAverageConfidences.Add(itemMetrics.AverageConfidence);
                            }
                        }
                    }

                    metricsDto.Items = new InvoiceFieldConfidenceDto
                    {
                        Found = metricsDto.ItemMetrics.Count > 0,
                        Confidence = AverageOrZero(itemAverageConfidences),
                        Inferred = false
                    };

                    if (metricsDto.Items.Confidence > 0)
                        globalConfidences.Add(metricsDto.Items.Confidence);

                    metricsDto.AverageConfidence = AverageOrZero(globalConfidences);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error crítico en OCR: {ex.Message}");
                throw;
            }

            return resultDto;
        }
    }
}