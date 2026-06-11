using R3.Data;
using R3.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace R3.Services;

public class RefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _opts;

    public RefreshTokenService(AppDbContext db, IOptions<JwtOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
    }

    public async Task<(string raw, RefreshToken row)> IssueAsync(long userId, CancellationToken ct)
    {
        var raw = TokenService.GenerateRefreshToken();
        var row = new RefreshToken
        {
            UserId = userId,
            TokenHash = TokenService.HashRefreshToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(_opts.RefreshTokenDays),
        };
        _db.RefreshTokens.Add(row);
        await _db.SaveChangesAsync(ct);
        return (raw, row);
    }

    public async Task<RefreshToken?> ValidateAsync(string raw, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(raw);
        var row = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        return row is { } r && r.IsActive ? r : null;
    }

    public async Task<(string newRaw, RefreshToken newRow)?> RotateAsync(string raw, CancellationToken ct)
    {
        var current = await ValidateAsync(raw, ct);
        if (current is null) return null;
        current.RevokedAt = DateTime.UtcNow;
        // IssueAsync's SaveChangesAsync flushes both the revoke above and the new row.
        return await IssueAsync(current.UserId, ct);
    }

    public async Task RevokeAsync(string raw, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(raw);
        var row = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (row is not null) { row.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
    }
}
