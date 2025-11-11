using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Overflow.Identity.Data
{
    // Heredamos de IdentityDbContext, usando los tipos base de Identity
    public class AppDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Aquí puedes agregar tus propias tablas además de las de Identity:
        // public DbSet<Product> Products { get; set; } = null!;
    }


}
