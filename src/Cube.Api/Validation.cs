using System.Reflection;
namespace Cube.Api;

public sealed class InputValidationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var nullability = new NullabilityInfoContext();
        foreach (var argument in context.Arguments)
        {
            if (argument is null || argument.GetType().Namespace != typeof(InputValidationFilter).Namespace || argument.GetType().GetMethod("<Clone>$") is null) continue;
            foreach (var property in argument.GetType().GetProperties())
            {
                var value = property.GetValue(argument);
                if (!property.PropertyType.IsValueType && nullability.Create(property).ReadState == NullabilityState.NotNull) Guard.That(value is not null, "missing_" + property.Name.ToLowerInvariant());
                if (value is string s) Guard.That(s.Length <= 10000, "field_too_long");
                if (value is Array a) Guard.That(a.Length <= 1000, "collection_too_large");
            }
        }
        return await next(context);
    }
}
