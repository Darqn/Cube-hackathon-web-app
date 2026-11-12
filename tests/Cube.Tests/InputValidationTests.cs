using Cube.Api;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Cube.Tests;

public sealed class InputValidationTests
{
    // The contract deliberately lives outside Cube.Api, as feature contracts will.
    private sealed record Input(string Name, string? Optional, Guid[] Ids) : IApiRequest;

    [Theory]
    [InlineData("missing")]
    [InlineData("string")]
    [InlineData("array")]
    public async Task Explicit_contracts_are_validated_independently_of_namespace(string invalid)
    {
        var input = new Input(invalid == "missing" ? null! : invalid == "string" ? new string('x', 10001) : "name",
            null, invalid == "array" ? new Guid[1001] : []);
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), input);
        var exception = await Assert.ThrowsAsync<ApiError>(async () =>
            await new InputValidationFilter().InvokeAsync(context, _ => ValueTask.FromResult<object?>(true)));
        Assert.Equal(invalid switch
        {
            "missing" => "missing_name",
            "string" => "field_too_long",
            _ => "collection_too_large"
        }, exception.Message);
    }

    [Fact]
    public async Task Optional_null_and_boundary_lengths_reach_the_endpoint()
    {
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new Input(new string('x', 10000), null, new Guid[1000]));
        Assert.Equal("accepted", await new InputValidationFilter().InvokeAsync(context, _ => ValueTask.FromResult<object?>("accepted")));
    }
}
