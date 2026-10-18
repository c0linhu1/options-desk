using Microsoft.EntityFrameworkCore;

namespace OptionsDesk.Data;

public class DeskDbContext(DbContextOptions<DeskDbContext> options) : DbContext(options)
{
    public DbSet<Underlying> Underlyings => Set<Underlying>();
    public DbSet<Trade> Trades => Set<Trade>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Underlying>(e =>
        {
            e.HasKey(u => u.Symbol);
            e.Property(u => u.Symbol).HasMaxLength(6);
            e.Property(u => u.Name).HasMaxLength(100);
            e.Property(u => u.Sector).HasMaxLength(50);
            e.Property(u => u.Spot).HasPrecision(18, 4);
            e.Ignore(u => u.Smile);
        });

        model.Entity<Trade>(e =>
        {
            e.Property(t => t.Symbol).HasMaxLength(21);
            e.Property(t => t.Underlying).HasMaxLength(6);
            e.Property(t => t.Side).HasConversion<string>().HasMaxLength(4);
            e.Property(t => t.Price).HasPrecision(18, 4);
            e.Property(t => t.Trader).HasMaxLength(50);
            e.Property(t => t.Account).HasMaxLength(20);
            e.HasOne<Underlying>().WithMany().HasForeignKey(t => t.Underlying);
            e.HasIndex(t => t.Underlying);
            e.ToTable(t => t.HasCheckConstraint("CK_Trades_Quantity", "Quantity > 0"));
            e.Ignore(t => t.IsOption);
        });
    }
}
