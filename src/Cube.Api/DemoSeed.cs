using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cube.Api;

public static class DemoSeed
{
    public const string Marker = "DemoSeed:v1";

    public static async Task Run(IServiceProvider services, IHostEnvironment environment, IConfiguration config)
    {
        if (!environment.IsDevelopment()) throw new InvalidOperationException("Demo data requires Development environment.");
        var db = services.GetRequiredService<CubeDb>();
        var users = services.GetRequiredService<UserManager<User>>();
        var storage = services.GetRequiredService<PrivateStorage>();
        if (!storage.AllowDevelopmentFiles) throw new InvalidOperationException("Enable Files__AllowUnscannedDevelopmentUploads for demo documents.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Serialize repeated/concurrent invocations; no existing demo edits are reset.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734028191)");
        if (await db.Audit.AnyAsync(x => x.Action == Marker))
        {
            Console.WriteLine("Demo dataset already exists; no records or passwords changed.");
            return;
        }
        var password = config["Demo:Password"] ?? throw new InvalidOperationException("Demo__Password required.");
        await Seed.Categories(db);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var writtenFiles = new List<string>();
        try
        {
            var tenants = new[]
            {
                new Tenant { Name = "ДЕМО · Студия Север", Inn = "9900000017", ContactName = "Анна Демонстрационная", Email = "north@example.test", Phone = "+7 000 000-00-01" },
                new Tenant { Name = "ДЕМО · Мастерская Восток", Inn = "9900000024", ContactName = "Иван Примеров", Email = "east@example.test", Phone = "+7 000 000-00-02" }
            };
            db.Tenants.AddRange(tenants);
            await db.SaveChangesAsync();
            async Task<User> AddUser(string login, string role, string name, Guid? tenant = null, bool approved = true)
            {
                var user = new User { Id = Guid.NewGuid(), UserName = login, Email = login + "@example.test", FullName = name, Role = role, TenantId = tenant, Approved = approved, PhoneNumber = "+7 000 000-00-00" };
                if (!approved) { user.RequestedOrganization = "ДЕМО · Новая компания"; user.RequestedInn = "9900000031"; }
                var result = await users.CreateAsync(user, password);
                if (!result.Succeeded) throw new InvalidOperationException($"Demo account {login}: {string.Join(", ", result.Errors.Select(x => x.Code))}");
                return user;
            }
            var admin = await AddUser("demo-admin", "Admin", "ДЕМО · Администратор");
            var staff = await AddUser("demo-staff", "Staff", "ДЕМО · Инженер Алексей");
            var north = await AddUser("demo-tenant", "Tenant", tenants[0].ContactName, tenants[0].Id);
            await AddUser("demo-tenant-2", "Tenant", tenants[1].ContactName, tenants[1].Id);
            await AddUser("demo-pending", "Tenant", "ДЕМО · Заявитель", approved: false);
            var categories = await db.Categories.OrderBy(x => x.Code).ToListAsync();
            // Fill unassigned routes only; preserve any existing staff assignments.
            foreach (var category in categories.Where(x => x.ResponsibleUserId is null)) category.ResponsibleUserId = staff.Id;
            for (var t = 0; t < tenants.Length; t++)
            {
                var tenant = tenants[t];
                var premises = new Premises { Name = $"ДЕМО · Офис {201 + t}", Address = "Демонстрационный проспект, 1", Building = "ДЕМО Центр", Section = "А", Floor = 2, Area = 64 + 24 * t, MonthlyRent = 64000 + 24000 * t, Status = "Occupied", Description = "Светлый офис с переговорной. Вымышленное помещение для демонстрации.", Utilities = "Электричество, вода, отопление" };
                var lease = new Lease { TenantId = tenant.Id, PremisesId = premises.Id, Number = $"DEMO-LEASE-{t + 1}", SignedOn = today.AddMonths(-4), StartsOn = today.AddMonths(-3), EndsOn = today.AddMonths(9) };
                db.AddRange(premises, lease);
                for (var i = 0; i < 4; i++)
                {
                    var amount = i == 3 ? 4200m : premises.MonthlyRent;
                    var paid = i == 0 ? amount : i == 2 ? amount / 2 : 0;
                    var invoice = new Invoice { TenantId = tenant.Id, ExternalId = $"demo:v1:{t}:{i}", Number = $"DEMO-{t + 1}-{i + 1:000}", Kind = i == 3 ? "Utilities" : "Rent", IssuedOn = today.AddDays(-35 + i * 10), DueOn = i == 1 ? today.AddDays(-7) : today.AddDays(10 + i), Amount = amount, Paid = paid, Purpose = i == 3 ? "ДЕМО · Коммунальные услуги" : "ДЕМО · Арендная плата" };
                    db.Add(invoice);
                    if (paid > 0) db.Add(new PaymentRecord { TenantId = tenant.Id, InvoiceId = invoice.Id, Provider = "DemoFixture", TransactionId = invoice.ExternalId, Amount = paid, Currency = "RUB", CreatedAt = now.AddDays(-3) });
                }
                foreach (var type in new[] { "Electricity", "Water" })
                {
                    var meter = new Meter { TenantId = tenant.Id, PremisesId = premises.Id, Type = type, SerialNumber = $"DEMO-{t + 1}-{type}", Unit = type == "Water" ? "m³" : "kWh", LastValue = 1300 };
                    db.Add(meter);
                    for (var r = 0; r < 3; r++) db.Add(new Reading { TenantId = tenant.Id, MeterId = meter.Id, PreviousValue = 1000 + r * 100, Value = 1100 + r * 100, Status = r == 2 ? "Submitted" : "Accepted", CreatedAt = now.AddMonths(r - 2) });
                }
                var subjects = new[] { "Заменить лампу в переговорной", "Проверить отопление офиса", "Согласовать перепланировку", "Уточнить начисления по аренде", "Выдать копию договора", "Обсудить продление аренды", "Повторная заявка на ремонт" };
                var states = Rules.RequestStates;
                string[] categoryCodes = ["electricity", "water-heating", "construction", "accounting", "rent", "termination", "construction"];
                for (var i = 0; i < states.Length; i++)
                {
                    var request = new ServiceRequest { TenantId = tenant.Id, PremisesId = premises.Id, CategoryId = categories.Single(x => x.Code == categoryCodes[i]).Id, AssigneeId = staff.Id, Subject = "ДЕМО · " + subjects[i], Description = "Демонстрационная заявка: можно просмотреть историю, написать сообщение и изменить статус.", Status = states[i], ContactName = tenant.ContactName, Phone = tenant.Phone, Email = tenant.Email, CreatedAt = now.AddDays(-8 + i), DueAt = now.AddDays(i - 2) };
                    db.Add(request);
                    if (i == 0) db.Add(new Notification { TenantId = tenant.Id, UserId = staff.Id, ResourceId = request.Id, Kind = "StaffRequestAssigned", Text = "ДЕМО · Вам назначена новая заявка" });
                    string[] path = states[i] switch
                    {
                        "New" => ["New"], "Accepted" => ["New", "Accepted"], "InProgress" => ["New", "Accepted", "InProgress"],
                        "Clarification" => ["New", "Accepted", "InProgress", "Clarification"],
                        "Completed" => ["New", "Accepted", "InProgress", "Completed"],
                        "Closed" => ["New", "Accepted", "InProgress", "Completed", "Closed"], _ => ["New", "Rejected"]
                    };
                    for (var e = 0; e < path.Length; e++) db.Add(new RequestEvent { TenantId = tenant.Id, RequestId = request.Id, ActorId = staff.Id, FromStatus = e == 0 ? "" : path[e - 1], ToStatus = path[e], Reason = "ДЕМО · История обработки", CreatedAt = request.CreatedAt.AddMinutes(e * 30) });
                    db.Add(new RequestMessage { TenantId = tenant.Id, RequestId = request.Id, AuthorId = staff.Id, Text = "Здравствуйте! Заявку получили, специалист свяжется с вами.", CreatedAt = request.CreatedAt.AddMinutes(5) });
                    db.Add(new RequestMessage { TenantId = tenant.Id, RequestId = request.Id, AuthorId = staff.Id, Text = "ДЕМО · Внутренняя заметка: согласовать время визита.", Internal = true, CreatedAt = request.CreatedAt.AddMinutes(10) });
                }
                var pdf = DemoPdf($"DEMO LEASE {t + 1} - FICTIONAL SAMPLE - NOT A LEGAL DOCUMENT");
                var file = new StoredFile { TenantId = tenant.Id, UploadedBy = admin.Id, Key = Guid.NewGuid().ToString("N"), Name = $"demo-lease-{t + 1}.pdf", ContentType = PrivateStorage.Validate(pdf, "demo.pdf"), Size = pdf.Length, State = "Ready" };
                Directory.CreateDirectory(storage.Root);
                var filePath = storage.PathFor(file.Key);
                writtenFiles.Add(filePath);
                await File.WriteAllBytesAsync(filePath, pdf);
                db.Add(file);
                db.Add(new Document { TenantId = tenant.Id, FileId = file.Id, LeaseId = lease.Id, PremisesId = premises.Id, Title = "ДЕМО · Договор аренды", Number = lease.Number, DocumentDate = lease.SignedOn });
                db.Add(new Notification { TenantId = tenant.Id, Kind = "InvoiceCreated", Text = "ДЕМО · Выставлен новый счёт за аренду", Read = false });
                db.Add(new Notification { TenantId = tenant.Id, Kind = "RequestUpdated", Text = "ДЕМО · Специалист принял вашу заявку", Read = true });
            }
            foreach (var type in new[] { "Office", "Retail", "Warehouse" })
            {
                var p = new Premises { Name = "ДЕМО · " + type, Type = type, Address = "Демонстрационный проспект, 1", Building = "ДЕМО Центр", Floor = 1, Area = type == "Warehouse" ? 180 : 75, MonthlyRent = type == "Warehouse" ? 90000 : 75000, Status = type == "Retail" ? "Reserved" : "Available", Description = "Вымышленное помещение для показа каталога аренды.", Utilities = "Электричество, отопление" };
                db.Add(p);
                db.Add(new Listing { PremisesId = p.Id, Title = p.Name, Description = p.Description, Published = true });
            }
            var announcement = new News { Title = "ДЕМО · Плановая проверка систем здания", Summary = "Просим подтвердить ознакомление", Body = "В демонстрационном бизнес-центре запланирован осмотр инженерных систем. Уведомление иллюстрирует подтверждение прочтения.", AllTenants = true, Importance = "Important", RequiresAcknowledgement = true, PublishAt = now.AddDays(-1) };
            db.Add(announcement);
            db.Add(new NewsReceipt { TenantId = tenants[0].Id, UserId = north.Id, NewsId = announcement.Id, ReadAt = now.AddHours(-1) });
            db.Add(new News { Title = "ДЕМО · Добро пожаловать в личный кабинет", Summary = "Заявки, счета и документы в одном месте", Body = "Это вымышленный набор данных для демонстрации функций приложения.", AllTenants = true, PublishAt = now.AddDays(-5) });
            db.Add(new News { Title = "ДЕМО · Встреча арендаторов", Summary = "Запланированная публикация", Body = "Новость станет доступна арендаторам завтра.", AllTenants = true, PublishAt = now.AddDays(1) });
            db.Add(new News { Title = "ДЕМО · Черновик объявления", Summary = "Видно администратору", Body = "Черновик для демонстрации публикации.", AllTenants = true });
            db.Audit.Add(new Audit { ActorId = admin.Id, Action = Marker, ResourceId = admin.Id });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            Console.WriteLine("Created demo data: 5 accounts, 2 tenants, 5 premises, 2 leases, 8 invoices, 4 payments, 14 requests, 4 meters, 12 readings, 2 PDF documents, 3 listings, 4 news items.");
        }
        catch
        {
            foreach (var path in writtenFiles) File.Delete(path);
            throw;
        }
    }

    private static byte[] DemoPdf(string text)
    {
        var stream = $"BT /F1 12 Tf 40 540 Td ({text}) Tj ET\n";
        string[] objects = ["<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 842 595] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {stream.Length} >>\nstream\n{stream}endstream"];
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++) { offsets.Add(pdf.Length); pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n"); }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append($"{offset:0000000000} 00000 n \n");
        pdf.Append($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
