namespace Cube.Api.Common.Identity;

public sealed class TenantContextRequiredException() : Exception("tenant_context_required");
