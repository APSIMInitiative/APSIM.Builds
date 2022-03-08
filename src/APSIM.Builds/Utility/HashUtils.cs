using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace APSIM.Builds.Utility;

/// <summary>
/// Utility functions for validating requests.
/// </summary>
public static class HashUtils
{
    /// <summary>
    /// Verify a request with HMAC SHA-256 hash.
    /// </summary>
    /// <param name="signature">Request signature.</param>
    /// <param name="key">Secret key for the hash algorithm.</param>
    /// <param name="stream">Request body stream.</param>
    public static async Task VerifyRequestHmac256Async(string signature, string key, Stream stream)
    {
        byte[] signatureBytes = Encoding.UTF8.GetBytes(signature);
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        await VerifyRequestHmac256Async(signatureBytes, keyBytes, stream);
    }

    /// <summary>
    /// Verify a request with HMAC SHA-256 hash.
    /// </summary>
    /// <param name="signature">Request signature.</param>
    /// <param name="key">Secret key for the hash algorithm.</param>
    /// <param name="stream">Request body stream.</param>
    public static async Task VerifyRequestHmac256Async(byte[] signature, byte[] key, Stream stream)
    {
        using (StreamReader reader = new StreamReader(stream, leaveOpen: true))
        using (HMACSHA256 hmac = new HMACSHA256(key))
        {
            string body = await reader.ReadToEndAsync();
            byte[] requestBytes = Encoding.UTF8.GetBytes(body);

            byte[] computedHash = hmac.ComputeHash(requestBytes);
            string hashHex = Convert.ToHexString(computedHash);
            string expectedHeader = $"sha256={hashHex.ToLowerInvariant()}";
            byte[] expectedHeaderBytes = Encoding.UTF8.GetBytes(expectedHeader);
            if (!HashesMatch(signature, expectedHeaderBytes))
                throw new CryptographicException("Request signature verification failed");
        }
    }

    /// <summary>
    /// Check if the two hashes match.
    /// </summary>
    /// <param name="expected">The first hash.</param>
    /// <param name="actual">The second hash.</param>
    private static bool HashesMatch(byte[] expected, byte[] actual)
    {
        if (expected.Length != actual.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
            if (expected[i] != actual[i])
                return false;

        return true;
    }
}
