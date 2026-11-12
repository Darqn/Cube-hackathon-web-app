namespace Cube.Api.Modules.Metering.Domain;

public static class ReadingRules
{
    public static bool CanSubmit(decimal previousValue, decimal value) => value >= previousValue && value <= 999999999999m;
}
