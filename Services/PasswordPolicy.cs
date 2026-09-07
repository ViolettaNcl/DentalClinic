namespace DentalClinic.Services;

public static class PasswordPolicy
{
    public const int MinimumLength = 10;

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength)
            return false;

        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
    }

    public static string ErrorMessage()
        => $"❌ Пароль должен содержать минимум {MinimumLength} символов, заглавную букву, строчную букву и цифру";
}
