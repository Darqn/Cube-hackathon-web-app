using Cube.Api.Common.Identity;

namespace Cube.Api.Modules.Metering;

public sealed class MeteringVisibility(ICurrentActor actor, CubeDb db)
{
    public IQueryable<Meter> Meters() => db.Meters.Where(m => actor.IsAdmin || db.Categories.Any(c =>
        c.ResponsibleUserId == actor.UserId && ((c.Code == "electricity" && m.Type == "Electricity") || (c.Code == "water-heating" && m.Type == "Water"))));

    public IQueryable<Reading> Readings()
    {
        var ids = Meters().Select(m => m.Id);
        return db.Readings.Where(r => ids.Contains(r.MeterId));
    }
}
