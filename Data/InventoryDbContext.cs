using Microsoft.EntityFrameworkCore;
using Models;

namespace Data
{
    public class InventoryDbContext : DbContext
    {
        public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<PendingOperation> PendingOperations { get; set; }
    }
}