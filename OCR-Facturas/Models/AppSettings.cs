using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas.Models
{
    public class AppSettings
    {
        public string AzureEndpoint { get; set; }
        public string AzureKey { get; set; }
        public string PostgresConnectionString { get; set; }
    }
}
