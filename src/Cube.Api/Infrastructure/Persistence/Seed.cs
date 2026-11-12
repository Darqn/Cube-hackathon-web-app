using Microsoft.EntityFrameworkCore;
namespace Cube.Api;

public static class Seed
{
    public static async Task Categories(CubeDb db)
    {
        var categories = new (string Code, string Name, string Department)[] { ("rent", "Вопрос по аренде", "Менеджер по аренде"), ("electricity", "Вопрос по электричеству", "Главный энергетик"), ("water-heating", "Водоснабжение и отопление", "Начальник службы ЭМО"), ("construction", "Вопрос по строительству", "Главный инженер"), ("accounting", "Вопрос по бухгалтерии", "Бухгалтер"), ("termination", "Расторжение договора аренды", "Отдел аренды") };
        foreach (var (code, name, department) in categories)
        if (!await db.Categories.AnyAsync(x => x.Code == code))
            db.Categories.Add(new Category { Code = code, Name = name, Department = department });
        await db.SaveChangesAsync();
    }
}
