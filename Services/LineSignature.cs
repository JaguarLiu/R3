using System.Security.Cryptography;
using System.Text;

namespace BudPay.Services;

public static class LineSignature
{
    public static bool Verify(string channelSecret, byte[] body, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
        var expected = Convert.ToBase64String(hmac.ComputeHash(body));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHeader));
    }
}
