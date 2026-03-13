using Microsoft.EntityFrameworkCore;
using OCR_Facturas.Models;
using OCR_Facturas.Services.Interface;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas.Services
{
    public class DatabaseService : IDatabaseService
    {
        private readonly AppSettings _settings;
        private bool _isInitialized = false; // Bandera para migrar solo una vez

        public DatabaseService(AppSettings settings)
        {
            _settings = settings;
        }

        private AppDbContext CreateContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(_settings.PostgresConnectionString);
            return new AppDbContext(optionsBuilder.Options);
        }


      
        

        public async Task SaveInvoiceAsync(InvoiceDto invoice)
        {
           
            using var db = CreateContext();
            await db.Invoices.AddAsync(invoice);
            
            await db.SaveChangesAsync();
            
            
        }

        public async Task<IEnumerable<InvoiceDto>> GetAllInvoicesAsync()
        {
            // Verificamos primero

            using var db = CreateContext();
            return await db.Invoices
                .Include(i => i.Items)
                .OrderByDescending(i => i.IssueDate)
                .ToListAsync();
        }
    }
}
