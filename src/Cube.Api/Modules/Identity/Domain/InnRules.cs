namespace Cube.Api.Modules.Identity.Domain;

public static class InnRules
{
    public static bool ValidInn(string value)
    {
        if ((value.Length != 10 && value.Length != 12) || value.Any(c => c < '0' || c > '9') || value.All(c => c == '0')) return false;
        int Check(int[] weights) => weights.Select((w, i) => w * (value[i] - '0')).Sum() % 11 % 10;
        return value.Length == 10 ? Check([2, 4, 10, 3, 5, 9, 4, 6, 8]) == value[9] - '0'
            : Check([7, 2, 4, 10, 3, 5, 9, 4, 6, 8]) == value[10] - '0' && Check([3, 7, 2, 4, 10, 3, 5, 9, 4, 6, 8]) == value[11] - '0';
    }
}
