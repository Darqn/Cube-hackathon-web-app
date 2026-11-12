using Cube.Api.Common.Identity;

namespace Cube.Api.Infrastructure.Persistence;

public sealed class TenantScope(ICurrentActor actor, CubeDb db)
{
    public Guid TenantId => actor.TenantId ?? throw new TenantContextRequiredException();
    public IQueryable<T> Own<T>() where T : Owned => db.Set<T>().Where(x => x.TenantId == TenantId);
}
