using BudPay.Models;
using Microsoft.EntityFrameworkCore;

namespace BudPay.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<SplitExpense> SplitExpenses => Set<SplitExpense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasColumnType("numeric(14,2)");
            e.Property(x => x.LineUserId).HasMaxLength(64);
            e.Property(x => x.LineGroupId).HasMaxLength(64);
            e.HasIndex(x => x.LineUserId);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<Trip>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.LineGroupId).HasMaxLength(64);
            e.HasIndex(x => x.LineGroupId);
            e.HasIndex(x => new { x.LineGroupId, x.IsActive });
            e.HasMany(x => x.Participants).WithOne().HasForeignKey(p => p.TripId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Expenses).WithOne().HasForeignKey(p => p.TripId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Participant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasIndex(x => x.TripId);
        });

        modelBuilder.Entity<SplitExpense>(e =>
        {
            e.Property(x => x.Day).HasMaxLength(50);
            e.Property(x => x.Item).HasMaxLength(200);
            e.Property(x => x.Total).HasColumnType("numeric(14,2)");
            e.Property(x => x.Payers).HasColumnType("jsonb");
            e.Property(x => x.Splits).HasColumnType("jsonb");
            e.HasIndex(x => x.TripId);
        });
    }
}
