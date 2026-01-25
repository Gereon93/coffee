using CoffeeApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace CoffeeApi.Infrastructure;

/// <summary>
/// EF Core DbContext for SQLite database
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MachineSnapshot> MachineSnapshots { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MachineSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.Property(e => e.MachineId)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("EQ900-DEFAULT");

            entity.Property(e => e.OperationState)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            // Indexes for performance
            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("IX_MachineSnapshots_Timestamp");

            entity.HasIndex(e => e.MachineId)
                .HasDatabaseName("IX_MachineSnapshots_MachineId");

            // Composite index for idempotency checks
            entity.HasIndex(e => new {
                    e.MachineId,
                    e.BeverageCounterCoffee,
                    e.BeverageCounterCoffeeAndMilk,
                    e.BeverageCounterMilk
                })
                .HasDatabaseName("IX_MachineSnapshots_Idempotency");

            // Ignore computed property
            entity.Ignore(e => e.TotalBeverages);
        });
    }
}
