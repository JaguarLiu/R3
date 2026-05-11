using BudPay.Models;
using Microsoft.EntityFrameworkCore;

namespace BudPay.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Expense> Expenses => Set<Expense>();

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
    }
}
