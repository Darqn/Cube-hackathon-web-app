namespace Cube.Api;

// Existing wire contracts retain their names and nesting for compatibility.
public static class AdminEndpoints
{
    public record TenantInput(string Name, string Inn, string ContactName, string Email, string Phone) : IApiRequest;
    public record Approval(Guid? ExistingTenantId) : IApiRequest;
    public record BlockInput(bool Blocked) : IApiRequest;
    public record StaffInput(string Login, string Email, string Password, string FullName) : IApiRequest;
    public record PremisesInput(string Name, string Type, string Address, string Building, string Section, int? Floor, decimal Area, decimal MonthlyRent, string Description, string Utilities, string Status) : IApiRequest;
    public record LeaseInput(Guid TenantId, Guid PremisesId, string Number, DateOnly SignedOn, DateOnly StartsOn, DateOnly EndsOn) : IApiRequest;
    public record LeaseStatus(string Status) : IApiRequest;
    public record MeterInput(Guid TenantId, Guid PremisesId, string Type, string SerialNumber, string Unit, decimal Baseline) : IApiRequest;
    public record MeterBaseline(decimal Value, string Reason, uint Version) : IApiRequest;
    public record Routing(Guid? ResponsibleUserId) : IApiRequest;
}
