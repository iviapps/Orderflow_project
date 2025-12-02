using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orderflow.Identity.Data
{
    // Solo para EF CLI (design-time). En runtime Aspire inyecta la conexión.
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>();

            // 1) Usa la env var si existe (p.ej. inyectada por Aspire/CI):
            var envConn = Environment.GetEnvironmentVariable("ConnectionStrings__identitydb");

            // 2) Si no hay env var, usa conexión fija local para la CLI/DBeaver: <- 
            //pero si usamos aspire no será necesaria, solo ha sido para la primera migración en la que no habia un auto migrate. 
            var conn = string.IsNullOrWhiteSpace(envConn)
                ? "Host=localhost;Port=5432;Database=identitydb;Username=postgres;Password=postgres;SSL Mode=Disable"
                : envConn;

            options.UseNpgsql(conn);
            return new AppDbContext(options.Options);
        }
    }
}
