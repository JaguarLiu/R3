using R3.Models;
using Microsoft.EntityFrameworkCore;

namespace R3.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<SplitExpense> SplitExpenses => Set<SplitExpense>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<TripMember> TripMembers => Set<TripMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Trip>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.LineGroupId).HasMaxLength(64);
            e.HasIndex(x => x.LineGroupId);
            e.HasIndex(x => new { x.LineGroupId, x.IsActive });
            e.HasIndex(x => x.OwnerUserId);
            e.HasIndex(x => x.ShareToken).IsUnique().HasFilter("\"ShareToken\" IS NOT NULL");
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
            e.Property(x => x.SourceChannel).HasMaxLength(10).HasDefaultValue("web");
            e.Property(x => x.CreatedByName).HasMaxLength(100);
            e.HasIndex(x => x.CreatedByUserId);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.LineUserId).HasMaxLength(64);
            e.HasIndex(x => x.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            e.HasIndex(x => x.LineUserId).IsUnique().HasFilter("\"LineUserId\" IS NOT NULL");
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(100);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<TripMember>(e =>
        {
            e.HasIndex(x => new { x.TripId, x.UserId }).IsUnique();
            e.HasIndex(x => x.UserId);
            // 一個 Participant 只能被一人認領（Postgres filtered unique index）
            e.HasIndex(x => new { x.TripId, x.ParticipantId }).IsUnique().HasFilter("\"ParticipantId\" IS NOT NULL");
            // 認領的 Participant 被刪時，成員保留但解除綁定
            e.HasOne<Participant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.SetNull);
        });

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            var dictConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<Dictionary<string, decimal>, string>(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
            modelBuilder.Entity<SplitExpense>().Property(x => x.Payers).HasConversion(dictConverter);
            modelBuilder.Entity<SplitExpense>().Property(x => x.Splits).HasConversion(dictConverter);
        }
    }
}
