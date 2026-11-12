namespace Cube.Api;

public static class Guard
{
    public static void That(bool condition, string code, int status = 400)
    {
        if (!condition)
            throw new ApiError(status, code);
    }
    public static string Text(string? value, string name, int max = 200)
    {
        That(!string.IsNullOrWhiteSpace(value) && value.Length <= max, $"invalid_{name}");
        return value!.Trim();
    }
    public static string Email(string value)
    {
        That(System.Net.Mail.MailAddress.TryCreate(value, out var mail) && mail.Address == value && value.Length <= 254, "invalid_email");
        return value;
    }
    public static int Page(int? page) => Math.Clamp(page ?? 1, 1, 100000);
    public static int Size(int? size) => Math.Clamp(size ?? 25, 1, 100);
}
