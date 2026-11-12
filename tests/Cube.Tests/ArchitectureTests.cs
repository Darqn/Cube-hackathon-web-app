using System.Reflection;
using System.Reflection.Emit;
using Cube.Api;
using Xunit;

namespace Cube.Tests;

public sealed class ArchitectureTests
{
    private static readonly Type[] Types = typeof(CubeDb).Assembly.GetTypes();
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(op => unchecked((ushort)op.Value));

    [Fact]
    public void Domain_types_do_not_reference_http_identity_or_persistence()
    {
        var domain = Types.Where(type => type.Namespace?.Contains(".Domain") == true || typeof(Entity).IsAssignableFrom(type));
        var violations = domain.SelectMany(type => Dependencies(type).Where(dependency =>
                dependency.Namespace?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true ||
                dependency.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true ||
                dependency == typeof(CubeDb) || dependency == typeof(Access) || dependency == typeof(ApiError))
            .Select(dependency => $"{type.FullName} -> {dependency.FullName}"));
        Assert.Empty(violations);
    }

    [Fact]
    public void Feature_handlers_and_workflows_have_no_http_or_service_locator_dependencies()
    {
        var application = Types.Where(type => type.Namespace?.Contains(".Features") == true || type.Namespace == "Cube.Api.Workflows");
        var violations = application.SelectMany(type => Dependencies(type).Where(dependency =>
                IsHttp(dependency) ||
                dependency == typeof(IServiceProvider) || dependency == typeof(Access) || dependency == typeof(Guard) ||
                dependency == typeof(ApiError) || dependency == typeof(IApiRequest) || dependency == typeof(OutcomeHttpExtensions) ||
                dependency.Name.EndsWith("Endpoint", StringComparison.Ordinal) || dependency.Name.EndsWith("Endpoints", StringComparison.Ordinal))
            .Select(dependency => $"{type.FullName} -> {dependency.FullName}"));
        Assert.True(!violations.Any(), string.Join("\n", violations));
    }

    [Fact]
    public void Dependencies_between_namespaced_modules_have_no_cycles()
    {
        var graph = Types.Where(type => Module(type) is not null).GroupBy(type => Module(type)!)
            .ToDictionary(group => group.Key, group => group.SelectMany(Dependencies).Select(Module)
                .OfType<string>().Where(name => name != group.Key).Distinct().ToArray());
        void Visit(string module, HashSet<string> path)
        {
            Assert.True(path.Add(module), "Module dependency cycle: " + string.Join(" -> ", path.Append(module)));
            foreach (var dependency in graph.GetValueOrDefault(module, [])) Visit(dependency, path);
            path.Remove(module);
        }
        foreach (var module in graph.Keys) Visit(module, []);
    }

    private static string? Module(Type type) => type.Namespace?.StartsWith("Cube.Api.Modules.", StringComparison.Ordinal) == true
        ? type.Namespace.Split('.')[3] : null;

    // Identity EF stores are part of persistence; direct EF queries are an explicit
    // choice for this single-project monolith. They are not HTTP transport types.
    private static bool IsHttp(Type type) => type.Namespace?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true
        && !type.Namespace.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal);

    // Inspect signatures AND IL, including async state machines, so hiding HTTP calls
    // inside a method body does not evade the boundary check. No extra NuGet dependency.
    private static IEnumerable<Type> Dependencies(Type type)
    {
        const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var found = new HashSet<Type>();
        void Add(Type? dependency)
        {
            if (dependency is null || !found.Add(dependency)) return;
            if (dependency.HasElementType) Add(dependency.GetElementType());
            foreach (var argument in dependency.GetGenericArguments()) Add(argument);
        }
        Add(type.BaseType);
        foreach (var implemented in type.GetInterfaces()) Add(implemented);
        foreach (var field in type.GetFields(flags)) Add(field.FieldType);
        foreach (var method in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
        {
            if (method is MethodInfo info) Add(info.ReturnType);
            foreach (var parameter in method.GetParameters()) Add(parameter.ParameterType);
            var body = method.GetMethodBody();
            if (body is null) continue;
            foreach (var local in body.LocalVariables) Add(local.LocalType);
            var il = body.GetILAsByteArray()!;
            for (var offset = 0; offset < il.Length;)
            {
                ushort value = il[offset++];
                if (value == 0xfe) value = (ushort)(0xfe00 | il[offset++]);
                var opcode = Opcodes[value];
                if (opcode.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType or OperandType.InlineTok)
                {
                    var member = method.Module.ResolveMember(BitConverter.ToInt32(il, offset), type.GetGenericArguments(), method.IsGenericMethod ? method.GetGenericArguments() : null);
                    Add(member as Type ?? member?.DeclaringType);
                }
                offset += opcode.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, offset),
                    _ => 4
                };
            }
        }
        return found;
    }
}
