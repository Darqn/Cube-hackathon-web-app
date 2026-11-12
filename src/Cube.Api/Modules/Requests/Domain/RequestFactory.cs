using System.Net.Mail;
using Cube.Api.Common.Operations;

namespace Cube.Api.Modules.Requests.Domain;

public sealed record RequestContent(Guid? PremisesId, string Subject, string Description, string ContactName, string Phone, string Email);

public static class RequestFactory
{
    public static Outcome<ServiceRequest> Create(RequestContent input, Guid tenant, Category category)
    {
        foreach (var (value, name, max) in new[]
        {
            (input.Subject, "subject", 200), (input.Description, "description", 10000),
            (input.ContactName, "contact", 200), (input.Phone, "phone", 40)
        })
            if (string.IsNullOrWhiteSpace(value) || value.Length > max)
                return Outcome<ServiceRequest>.Reject(Failure.Invalid("invalid_" + name));

        if (!MailAddress.TryCreate(input.Email, out var email) || email.Address != input.Email || input.Email.Length > 254)
            return Outcome<ServiceRequest>.Reject(Failure.Invalid("invalid_email"));

        return Outcome<ServiceRequest>.Success(new ServiceRequest
        {
            TenantId = tenant, CategoryId = category.Id, AssigneeId = category.ResponsibleUserId,
            PremisesId = input.PremisesId, Subject = input.Subject.Trim(), Description = input.Description.Trim(),
            ContactName = input.ContactName.Trim(), Phone = input.Phone.Trim(), Email = input.Email
        });
    }
}
