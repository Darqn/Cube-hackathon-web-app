using System.Collections.Concurrent;
using System.Reflection;

namespace Cube.Api;

public sealed class InputValidationFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<Type, PropertyRule[]> Rules = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is not IApiRequest) continue;

            foreach (var rule in Rules.GetOrAdd(argument.GetType(), Describe))
            {
                var value = rule.Property.GetValue(argument);
                if (rule.Required) Guard.That(value is not null, rule.MissingCode);
                if (value is string text) Guard.That(text.Length <= 10000, "field_too_long");
                if (value is Array array) Guard.That(array.Length <= 1000, "collection_too_large");
            }
        }

        return await next(context);
    }

    private static PropertyRule[] Describe(Type type)
    {
        var nullability = new NullabilityInfoContext();
        return type.GetProperties().Select(property => new PropertyRule(
            property,
            !property.PropertyType.IsValueType && nullability.Create(property).ReadState == NullabilityState.NotNull,
            "missing_" + property.Name.ToLowerInvariant())).ToArray();
    }

    private sealed record PropertyRule(PropertyInfo Property, bool Required, string MissingCode);
}
