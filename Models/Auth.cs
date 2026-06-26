namespace R3.Models;

public class User
{
    public long Id { get; set; }
    public string? Email { get; set; }          // unique when present
    public string? PasswordHash { get; set; }   // null for LINE-only accounts
    public string DisplayName { get; set; } = "";
    public string? LineUserId { get; set; }      // unique when present
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string TokenHash { get; set; } = "";  // SHA-256 of the opaque token
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTime.UtcNow;
}

public class TripMember
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public long UserId { get; set; }
    public long? ParticipantId { get; set; }   // 認領的虛擬名稱；null = 未綁定
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
