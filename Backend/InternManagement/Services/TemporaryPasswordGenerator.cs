using System.Security.Cryptography;

namespace InternManagement.Services;

public static class TemporaryPasswordGenerator
{
    public static string Generate()
    {
        string[] groups = ["ABCDEFGHJKLMNPQRSTUVWXYZ", "abcdefghjkmnpqrstuvwxyz", "23456789", "!@#$%&*?"];
        var alphabet = string.Concat(groups);
        var chars = new char[10];
        for (var i = 0; i < groups.Length; i++) chars[i] = groups[i][RandomNumberGenerator.GetInt32(groups[i].Length)];
        for (var i = groups.Length; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var index = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[index]) = (chars[index], chars[i]);
        }
        return new string(chars);
    }
}
