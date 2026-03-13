using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;

namespace OCR_Facturas
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // Ponemos una cadena de conexión "falsa" solo para que la herramienta 
            // lea los modelos y genere el código de la migración.
            var dummyConnectionString = "Host=localhost;Database=postgres;Username=postgres;Password=prueba1234";
            optionsBuilder.UseNpgsql(dummyConnectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
