using System;
using System.Security.Cryptography;

/// <summary>
/// PBKDF2 password hashing. Salt and hash are stored base64-encoded in TEXT
/// columns so the Access database only ever holds ASCII.
/// </summary>
public static class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 25000;

    public static void CreateHash(string password, out string hash, out string salt)
    {
        byte[] saltBuffer = new byte[SaltBytes];
        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            rng.GetBytes(saltBuffer);

        salt = Convert.ToBase64String(saltBuffer);
        hash = Convert.ToBase64String(Derive(password, saltBuffer));
    }

    public static bool Verify(string password, string hash, string salt)
    {
        if (String.IsNullOrEmpty(hash) || String.IsNullOrEmpty(salt)) return false;

        try
        {
            byte[] saltBuffer = Convert.FromBase64String(salt);
            byte[] expected = Convert.FromBase64String(hash);

            // A row that has been truncated or hand-edited should fail the sign-in,
            // not throw an error page at whoever is trying to get in.
            if (saltBuffer.Length < 8 || expected.Length == 0) return false;

            return FixedTimeEquals(expected, Derive(password, saltBuffer));
        }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
    }

    private static byte[] Derive(string password, byte[] salt)
    {
        using (Rfc2898DeriveBytes pbkdf2 =
                   new Rfc2898DeriveBytes(password ?? "", salt, Iterations))
        {
            return pbkdf2.GetBytes(HashBytes);
        }
    }

    /// <summary>Comparison whose running time does not depend on where the bytes differ.</summary>
    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
