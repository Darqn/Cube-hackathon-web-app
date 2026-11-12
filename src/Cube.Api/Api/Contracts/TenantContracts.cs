namespace Cube.Api;

public static class TenantEndpoints
{
    public record ListingInput(Guid PremisesId, string Title, string Description, bool Published) : IApiRequest;
    public record InquiryInput(string ContactName, string Phone, string Email, string Message) : IApiRequest;
    public record InvoiceInput(Guid TenantId, string Number, string Kind, DateOnly IssuedOn, DateOnly DueOn, string Purpose, decimal Amount, Guid? FileId) : IApiRequest;
}
