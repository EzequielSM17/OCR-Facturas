using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;

namespace OCR_Facturas.Services
{
    public class AzureOcrService : IAzureOcrService
    {
        private readonly AppSettings _settings;

        public AzureOcrService(AppSettings settings)
        {
            _settings = settings;
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

            // Intenta cultura española
            if (decimal.TryParse(input, NumberStyles.Any, new CultureInfo("es-ES"), out var valueEs))
                return valueEs;

            // Intenta cultura invariante
            if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var valueInv))
                return valueInv;

            // Limpieza básica: dejar solo dígitos, coma, punto y signo
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

            // Ejemplos que cubre:
            // "2 uds"
            // "1,5 kg"
            // "3 h"
            // "12 x tornillo"
            var match = Regex.Match(texto, @"(?<!\d)(\d+[.,]?\d*)\s*(uds?|unid(?:ades)?|kg|g|l|ml|h|hr|hrs|hora(?:s)?|u)?\b",
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


        public async Task<InvoiceDto> ExtractInvoiceDataAsync(FileStream imageStream)
            {
                // 1. Inicializamos tu objeto, listo para guardar en la base de datos
                var invoiceDto = new InvoiceDto();

                try
                {
                    // 2. Creamos el cliente exactamente como tu compañero
                    var credential = new AzureKeyCredential(_settings.AzureKey);
                    var client = new DocumentAnalysisClient(new Uri(_settings.AzureEndpoint), credential);

                    // 3. Llamamos al modelo "prebuilt-invoice"
                    AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-invoice", imageStream);
                    AnalyzeResult result = operation.Value;

                    if (result.Documents.Count > 0)
                    {
                        AnalyzedDocument document = result.Documents[0];

                    // --- EXTRACCIÓN DE DATOS GENERALES ---

                    
                        invoiceDto.LocalImagePath = Path.GetFileName(imageStream?.Name);
                    
                    

                        // Fecha (InvoiceDate -> IssueDate) - Con el truco UTC para PostgreSQL
                        if (document.Fields.TryGetValue("InvoiceDate", out DocumentField dateField) && dateField.FieldType == DocumentFieldType.Date)
                        {
                            var extractedDate = dateField.Value.AsDate().DateTime;
                            invoiceDto.IssueDate = DateTime.SpecifyKind(extractedDate, DateTimeKind.Utc);
                        }
                        else
                        {
                            invoiceDto.IssueDate = DateTime.UtcNow; // Fecha de seguridad por si no hay fecha en el papel
                        }

                        // Total a Pagar (InvoiceTotal -> TotalAmount) - Usando el truco Currency de tu compañero
                        if (document.Fields.TryGetValue("InvoiceTotal", out DocumentField totalField))
                        {
                            if (totalField.FieldType == DocumentFieldType.Double)
                                invoiceDto.TotalAmount = (decimal)totalField.Value.AsDouble();
                            else if (totalField.FieldType == DocumentFieldType.Currency)
                                invoiceDto.TotalAmount = (decimal)totalField.Value.AsCurrency().Amount;
                        }

                    // Subtotal (Base Imponible)
                    if (document.Fields.TryGetValue("VendorName", out DocumentField vendorName) && vendorName.FieldType == DocumentFieldType.String)
                    {
                        invoiceDto.MerchantName = vendorName.Value.AsString();
                    }



                    // --- EXTRACCIÓN DE LA TABLA DE PRODUCTOS ---
                    if (document.Fields.TryGetValue("Items", out DocumentField itemsField) &&
    itemsField.FieldType == DocumentFieldType.List)
                    {
                        foreach (DocumentField itemField in itemsField.Value.AsList())
                        {
                            if (itemField.FieldType != DocumentFieldType.Dictionary)
                                continue;

                            var itemDict = itemField.Value.AsDictionary();
                            var invoiceItem = new InvoiceItemDto();

                            decimal? amount = null;
                            decimal? unitPrice = null;
                            decimal? quantity = null;

                            // Descripción
                            if (itemDict.TryGetValue("Description", out DocumentField descField) &&
                                descField.FieldType == DocumentFieldType.String)
                            {
                                invoiceItem.Description = descField.Value.AsString();
                            }

                            // Cantidad
                            if (itemDict.TryGetValue("Quantity", out DocumentField quantityField))
                            {
                                quantity = GetDecimalFromField(quantityField);
                            }

                            // Precio unitario
                            if (itemDict.TryGetValue("UnitPrice", out DocumentField unitPriceField))
                            {
                                unitPrice = GetDecimalFromField(unitPriceField);
                            }

                            // Total línea
                            if (itemDict.TryGetValue("Amount", out DocumentField amountField))
                            {
                                amount = GetDecimalFromField(amountField);
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
                            }

                            // Fallback de cantidad:
                            // 1) usar campo Quantity
                            // 2) si no viene, calcular Amount / UnitPrice
                            // 3) si sigue sin salir, intentar sacarla de la descripción
                            if (quantity.HasValue && quantity.Value > 0)
                            {
                                invoiceItem.Quantity = (double)quantity.Value;
                            }
                            else if (amount.HasValue && unitPrice.HasValue && unitPrice.Value > 0)
                            {
                                invoiceItem.Quantity = (double)decimal.Round(amount.Value / unitPrice.Value, 3);
                            }
                            else
                            {
                                var qtyFromDescription = ExtraerCantidadDesdeTexto(invoiceItem.Description);
                                if (qtyFromDescription.HasValue)
                                    invoiceItem.Quantity = (double)qtyFromDescription.Value;
                            }

                            if (unitPrice.HasValue)
                                invoiceItem.UnitPrice = unitPrice.Value;

                            if (amount.HasValue)
                                invoiceItem.TotalPrice = amount.Value;

                            // Si no vino Amount pero sí cantidad y precio unitario
                            if (invoiceItem.TotalPrice <= 0 &&
                                invoiceItem.Quantity > 0 &&
                                invoiceItem.UnitPrice > 0)
                            {
                                invoiceItem.TotalPrice = (decimal)invoiceItem.Quantity * invoiceItem.UnitPrice;
                            }

                            if (!string.IsNullOrWhiteSpace(invoiceItem.Description) || invoiceItem.TotalPrice > 0)
                            {
                                invoiceDto.Items.Add(invoiceItem);
                            }
                        }
                    }



                }

            }
                catch (Exception ex)
                {
                    // Mostramos el error en consola para que no se oculte
                    Console.WriteLine($"Error crítico en OCR: {ex.Message}");
                    throw; // Dejamos que el ViewModel maneje el error y muestre la alerta
                }

                return invoiceDto;
            }

        
    }
    }


