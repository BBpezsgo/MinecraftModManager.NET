using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MMM;

public static class Utils
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
    };

    public static async Task<string> ComputeHash256(string file, CancellationToken ct)
    {
        using FileStream fileStream = File.Open(file, FileMode.Open);
        fileStream.Position = 0;
        return await ComputeHash256(fileStream, ct);
    }

    public static async Task<string> ComputeHash1(string file, CancellationToken ct)
    {
        using FileStream fileStream = File.Open(file, FileMode.Open);
        fileStream.Position = 0;
        return await ComputeHash1(fileStream, ct);
    }

    public static async Task<string> ComputeHash256(Stream stream, CancellationToken ct)
    {
        using SHA256 algorithm = SHA256.Create();
        byte[] hash = await algorithm.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static async Task<string> ComputeHash1(Stream stream, CancellationToken ct)
    {
        using SHA1 algorithm = SHA1.Create();
        byte[] hash = await algorithm.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string SanitizeJson(string input)
    {
        StringBuilder builder = new();

        bool inString = false;
        bool escapeNext = false;
        foreach (char c in input)
        {
            if (c == '"' && !escapeNext) inString = !inString;

            escapeNext = false;

            if (inString)
            {
                if (c == '\n') builder.Append("\\n");
                else if (c == '\r') builder.Append("\\r");
                else if (c == '\\')
                {
                    escapeNext = true;
                    builder.Append('\\');
                }
                else
                {
                    builder.Append(c);
                }
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}