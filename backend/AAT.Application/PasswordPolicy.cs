using System.Net.Mail;

namespace AAT.Application;

public static class PasswordPolicy
{
    public static string? Validate(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 12)
            return "Password must be at least 12 characters.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return "Password must include a letter and a digit.";
        return null;
    }

    public static bool IsEmail(string email)
    {
        if (email.Length is < 3 or > 256 || email.Any(char.IsWhiteSpace))
            return false;
        return MailAddress.TryCreate(email, out var parsed) && parsed.Address.Equals(email, StringComparison.OrdinalIgnoreCase);
    }
}
