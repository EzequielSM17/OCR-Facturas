using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
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
                    if (document.Fields.TryGetValue("Items", out DocumentField itemsField) && itemsField.FieldType == DocumentFieldType.List)
                        {
                            foreach (DocumentField itemField in itemsField.Value.AsList())
                            {
                                if (itemField.FieldType == DocumentFieldType.Dictionary)
                                {
                                    var itemDict = itemField.Value.AsDictionary();
                                    var invoiceItem = new InvoiceItemDto();
                                    // Descripción
                                    if (itemDict.TryGetValue("Description", out DocumentField descField) && descField.FieldType == DocumentFieldType.String)
                                        invoiceItem.Description = descField.Value.AsString();

                                // Cantidad
                                if (itemDict.TryGetValue("Amount", out DocumentField amount))
                                {
                                    if (amount.FieldType == DocumentFieldType.Double)
                                        invoiceItem.Quantity = amount.Value.AsDouble();
                                    else if (amount.FieldType == DocumentFieldType.Currency)
                                        invoiceItem.Quantity = amount.Value.AsCurrency().Amount;
                                }

                                // Precio Unitario
                                if (itemDict.TryGetValue("UnitPrice", out DocumentField unitPriceField))
                                    {
                                        if (unitPriceField.FieldType == DocumentFieldType.Double)
                                            invoiceItem.UnitPrice = (decimal)unitPriceField.Value.AsDouble();
                                        else if (unitPriceField.FieldType == DocumentFieldType.Currency)
                                            invoiceItem.UnitPrice = (decimal)unitPriceField.Value.AsCurrency().Amount;
                                    }

                                    // Total de la línea (Amount)
                                    if (itemDict.TryGetValue("Amount", out DocumentField amountField))
                                    {
                                        if (amountField.FieldType == DocumentFieldType.Double)
                                            invoiceItem.TotalPrice = (decimal)amountField.Value.AsDouble();
                                        else if (amountField.FieldType == DocumentFieldType.Currency)
                                            invoiceItem.TotalPrice = (decimal)amountField.Value.AsCurrency().Amount;
                                    }
                                if (itemDict.TryGetValue("Tax", out DocumentField taxField) && taxField.FieldType == DocumentFieldType.String)
                                {
                                    string tax = taxField.Value.AsString().Split(" ")[0];
                                    invoiceItem.IvaAmount = int.Parse(tax);
                                }
                                // Solo añadimos a la base de datos si tiene texto o algún precio
                                if (!string.IsNullOrEmpty(invoiceItem.Description) || invoiceItem.TotalPrice > 0)
                                    {
                                        invoiceDto.Items.Add(invoiceItem);
                                    }
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


