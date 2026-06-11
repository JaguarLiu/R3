using System.Security.Cryptography;
using R3.Auth;
using R3.Common;
using R3.Data;
using R3.Models;
using R3.Providers;
using R3.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace R3.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshCookie = "r3_refresh";
    private const string StateCookie = "r3_oauth_state";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").RequireRateLimiting("auth");

        auth.MapPost("/register", async ([FromBody] RegisterDto dto, AppDbContext db,
            TokenService tokens, RefreshTokenService refresh, HttpResponse res, CancellationToken ct) =>
        {
            if (dto is null) return Results.BadRequest(new { error = "invalid_body" });
            if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains('@'))
                return Results.BadRequest(new { error = "invalid_email" });
            if (string.IsNullOrEmpty(dto.Password) || dto.Password.Length < 8)
                return Results.BadRequest(new { error = "weak_password" });
            if (string.IsNullOrWhiteSpace(dto.DisplayName))
                return Results.BadRequest(new { error = "display_name_required" });

            var email = dto.Email.Trim().ToLowerInvariant();
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
                return Results.Conflict(new { error = "email_taken" });

            var user = new User
            {
                Email = email,
                PasswordHash = Passwords.Hash(dto.Password),
                DisplayName = dto.DisplayName.Trim(),
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return await IssueSession(user, tokens, refresh, res, ct);
        });

        auth.MapPost("/login", async ([FromBody] LoginDto dto, AppDbContext db,
            TokenService tokens, RefreshTokenService refresh, HttpResponse res, CancellationToken ct) =>
        {
            if (dto is null) return Results.Json(new { error = "invalid_credentials" }, statusCode: 401);
            var email = (dto.Email ?? "").Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user?.PasswordHash is null || !Passwords.Verify(dto.Password ?? "", user.PasswordHash))
                return Results.Json(new { error = "invalid_credentials" }, statusCode: 401);
            return await IssueSession(user, tokens, refresh, res, ct);
        });

        auth.MapGet("/line/start", (LineLoginClient line, HttpResponse res) =>
        {
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            res.Cookies.Append(StateCookie, state, new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
                Path = "/api/auth", MaxAge = TimeSpan.FromMinutes(10),
            });
            return Results.Redirect(line.BuildAuthorizeUrl(state));
        });

        auth.MapGet("/line/callback", async (string? code, string? state, HttpRequest req,
            HttpResponse res, LineLoginClient line, AppDbContext db,
            TokenService tokens, RefreshTokenService refresh, CancellationToken ct) =>
        {
            var expected = req.Cookies[StateCookie];
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state) || state != expected)
                return Results.BadRequest(new { error = "invalid_oauth_state" });
            res.Cookies.Delete(StateCookie, new CookieOptions { Path = "/api/auth" });

            LineLoginClient.LineProfile profile;
            try { profile = await line.ExchangeAsync(code, ct); }
            catch (Exception) { return Results.BadRequest(new { error = "line_login_failed" }); }
            var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == profile.UserId, ct);
            if (user is null)
            {
                user = new User { LineUserId = profile.UserId, DisplayName = profile.DisplayName };
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
            }
            await IssueSession(user, tokens, refresh, res, ct);
            // Send the browser back to the SPA; the SPA bootstraps via /refresh.
            return Results.Redirect("/");
        });

        auth.MapPost("/refresh", async (HttpRequest req, HttpResponse res,
            RefreshTokenService refresh, AppDbContext db, TokenService tokens, CancellationToken ct) =>
        {
            var raw = req.Cookies[RefreshCookie];
            if (string.IsNullOrEmpty(raw)) return Results.Json(new { error = "no_refresh" }, statusCode: 401);
            var rotated = await refresh.RotateAsync(raw, ct);
            if (rotated is null) return Results.Json(new { error = "invalid_refresh" }, statusCode: 401);

            SetRefreshCookie(res, rotated.Value.newRaw, rotated.Value.newRow.ExpiresAt);
            var user = await db.Users.FindAsync(new object[] { rotated.Value.newRow.UserId }, ct);
            if (user is null) return Results.Json(new { error = "invalid_refresh" }, statusCode: 401);
            return Results.Ok(new { accessToken = tokens.CreateAccessToken(user.Id, user.DisplayName) });
        });

        auth.MapPost("/logout", async (HttpRequest req, HttpResponse res,
            RefreshTokenService refresh, CancellationToken ct) =>
        {
            var raw = req.Cookies[RefreshCookie];
            if (!string.IsNullOrEmpty(raw)) await refresh.RevokeAsync(raw, ct);
            res.Cookies.Delete(RefreshCookie, new CookieOptions { Path = "/api/auth" });
            return Results.NoContent();
        });

        auth.MapGet("/me", async (System.Security.Claims.ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.CurrentUserId();
            if (userId is null) return Results.Unauthorized();
            var user = await db.Users.FindAsync(new object[] { userId.Value }, ct);
            return user is null ? Results.Unauthorized()
                : Results.Ok(new { user.Id, user.Email, user.DisplayName, lineLinked = user.LineUserId != null });
        }).RequireAuthorization();
    }

    private static async Task<IResult> IssueSession(User user, TokenService tokens,
        RefreshTokenService refresh, HttpResponse res, CancellationToken ct)
    {
        var (raw, row) = await refresh.IssueAsync(user.Id, ct);
        SetRefreshCookie(res, raw, row.ExpiresAt);
        return Results.Ok(new { accessToken = tokens.CreateAccessToken(user.Id, user.DisplayName) });
    }

    private static void SetRefreshCookie(HttpResponse res, string raw, DateTime expires) =>
        res.Cookies.Append(RefreshCookie, raw, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
            Path = "/api/auth", Expires = expires,
        });
}

public record RegisterDto(string Email, string Password, string DisplayName);
public record LoginDto(string Email, string Password);
