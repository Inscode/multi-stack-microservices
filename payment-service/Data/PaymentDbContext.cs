using Microsoft.EntityFrameworkCore;

namespace PaymentService.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) {}

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey( x => x.Id);

            e.Property(x => x.Amount).HasColumnType("numeric(12, 2)");
            e.Property(x => x.status).HasMaxLength(20);
            e.HasIndex(x => x.OrderId)
            .IsUnique();
        });
    }

}