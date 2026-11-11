using System.Security.Cryptography;
using System.Text;

namespace IAPropia.Security;

public static class ApiKeyHelper
{
    public static bool Coincide(string? expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
            return false;

        var a = Encoding.UTF8.GetBytes(expected.Trim());
        var b = Encoding.UTF8.GetBytes(provided.Trim());
        if (a.Length != b.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
