using Cube.Api;
using Cube.Api.Modules.Metering.Domain;
using Cube.Api.Modules.Requests.Domain;
using Xunit;

namespace Cube.Tests;

public sealed class RequestDomainTests
{
    [Fact]
    public void Factory_applies_routing_and_normalizes_text_without_changing_email()
    {
        var tenant = Guid.NewGuid();
        var premises = Guid.NewGuid();
        var category = new Category { ResponsibleUserId = Guid.NewGuid() };
        var result = RequestFactory.Create(new(premises, " Issue ", " Description ", " Contact ", " 123 ", "Person@example.test"), tenant, category);
        Assert.Null(result.Error);
        var request = result.Value;
        Assert.Equal(tenant, request.TenantId);
        Assert.Equal(category.Id, request.CategoryId);
        Assert.Equal(category.ResponsibleUserId, request.AssigneeId);
        Assert.Equal(premises, request.PremisesId);
        Assert.Equal("Issue", request.Subject);
        Assert.Equal("Description", request.Description);
        Assert.Equal("Contact", request.ContactName);
        Assert.Equal("123", request.Phone);
        Assert.Equal("Person@example.test", request.Email);
        Assert.Equal("New", request.Status);
    }

    [Theory]
    [InlineData("", "text", "contact", "phone", "a@example.test", "invalid_subject")]
    [InlineData("text", " ", "contact", "phone", "a@example.test", "invalid_description")]
    [InlineData("text", "text", " ", "phone", "a@example.test", "invalid_contact")]
    [InlineData("text", "text", "contact", " ", "a@example.test", "invalid_phone")]
    [InlineData("text", "text", "contact", "phone", "not email", "invalid_email")]
    public void Invalid_content_returns_a_business_failure(string subject, string description, string contact, string phone, string email, string code)
    {
        var result = RequestFactory.Create(new(null, subject, description, contact, phone, email), Guid.NewGuid(), new Category());
        Assert.Equal(code, result.Error?.Code);
    }

    [Theory]
    [InlineData(10, 9, false)]
    [InlineData(10, 10, true)]
    [InlineData(10, 11, true)]
    [InlineData(10, 999999999999, true)]
    [InlineData(10, 1000000000000, false)]
    public void Reading_watermark_and_upper_bound_are_inclusive(long previous, long value, bool expected) =>
        Assert.Equal(expected, ReadingRules.CanSubmit(previous, value));
}
