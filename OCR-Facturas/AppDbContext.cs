using Microsoft.EntityFrameworkCore;
using OCR_Facturas.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<InvoiceDto> Invoices { get; set; }
        public DbSet<InvoiceItemDto> InvoiceItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relación Uno a Muchos
            modelBuilder.Entity<InvoiceDto>()
                .HasMany(i => i.Items)
                .WithOne()
                .HasForeignKey(i => i.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    
    }
}
