using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cube.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Cube.Tests;

public sealed class EmployeeTests(ApiFixture f) : IClassFixture<ApiFixture>
{
    private static MultipartFormDataContent Reply(string text, bool internalNote, string name = "reply.pdf", byte[]? bytes = null)
    {
        var form = new MultipartFormDataContent(); form.Add(new StringContent(text), "text"); form.Add(new StringContent(internalNote.ToString()), "internal");
        form.Add(new ByteArrayContent(bytes ?? "%PDF-1.4\nEmployee reply"u8.ToArray()), "file", name); return form;
    }
    private async Task<Guid?> Route(Guid? user)
    {
        using var s = f.Services.CreateScope(); var db = s.ServiceProvider.GetRequiredService<CubeDb>();
        var c = await db.Categories.SingleAsync(c => c.Code == "electricity"); var old = c.ResponsibleUserId; c.ResponsibleUserId = user; await db.SaveChangesAsync(); return old;
    }
    private async Task<Guid> Reading()
    {
        using var s = f.Services.CreateScope(); var db = s.ServiceProvider.GetRequiredService<CubeDb>();
        var m = new Meter { TenantId = f.TenantA, PremisesId = f.PremisesA, SerialNumber = Guid.NewGuid().ToString(), Type = "Electricity", Unit = "kWh", LastValue = 22 };
        var r = new Reading { TenantId = f.TenantA, MeterId = m.Id, PreviousValue = 10, Value = 22 }; db.Meters.Add(m); db.Readings.Add(r); await db.SaveChangesAsync(); return r.Id;
    }
    [Fact]
    public async Task Employee_endpoints_require_employee_role()
    {
        using var guest = f.CreateClient(); using var tenant = await f.As("tenant-a"); using var staff = await f.As("staff");
        foreach (var path in new[] { "/api/v1/staff/dashboard", "/api/v1/staff/notifications" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await tenant.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync(path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await tenant.PostAsync("/api/v1/staff/notifications/read-all", null)).StatusCode);
        using var form = Reply("text", false); Assert.Equal(HttpStatusCode.Forbidden, (await tenant.PostAsync($"/api/v1/staff/requests/{f.RequestA}/replies", form)).StatusCode);
    }
    [Fact]
    public async Task Public_and_internal_reply_files_have_distinct_tenant_visibility()
    {
        using var staff = await f.As("staff"); using var tenant = await f.As("tenant-a"); using var other = await f.As("tenant-b");
        using (var form = Reply("PUBLIC FILE REPLY", false)) (await staff.PostAsync($"/api/v1/staff/requests/{f.RequestA}/replies", form)).EnsureSuccessStatusCode();
        using (var form = Reply("PRIVATE FILE REPLY", true, "internal-only.pdf")) (await staff.PostAsync($"/api/v1/staff/requests/{f.RequestA}/replies", form)).EnsureSuccessStatusCode();
        var visible = await tenant.GetStringAsync($"/api/v1/requests/{f.RequestA}"); Assert.Contains("PUBLIC FILE REPLY", visible); Assert.DoesNotContain("PRIVATE FILE REPLY", visible); Assert.DoesNotContain("internal-only.pdf", visible);
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/requests/{f.RequestA}"); var files = detail.GetProperty("attachments").EnumerateArray().ToArray();
        var hiddenId = files.Single(a => a.GetProperty("name").GetString() == "internal-only.pdf").GetProperty("fileId").GetGuid();
        var publicId = files.Single(a => a.GetProperty("name").GetString() == "reply.pdf").GetProperty("fileId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await tenant.GetAsync($"/api/v1/files/{hiddenId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/files/{hiddenId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tenant.GetAsync($"/api/v1/files/{publicId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/files/{publicId}")).StatusCode);
        using var denied = Reply("not assigned", false); Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsync($"/api/v1/staff/requests/{f.RequestB}/replies", denied)).StatusCode);
    }
    [Fact]
    public async Task Invalid_reply_file_does_not_create_a_message_or_partial_attachment()
    {
        using var staff = await f.As("staff");
        using var form = Reply("INVALID ATTACHMENT MUST NOT SAVE", false, "bad.pdf", "invalid signature"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsync($"/api/v1/staff/requests/{f.RequestA}/replies", form)).StatusCode);
        using var s = f.Services.CreateScope(); var db = s.ServiceProvider.GetRequiredService<CubeDb>(); Assert.False(await db.Messages.AnyAsync(m => m.Text == "INVALID ATTACHMENT MUST NOT SAVE"));
    }
    [Fact]
    public async Task Notification_access_is_revoked_when_request_is_reassigned()
    {
        using var admin = await f.As("admin"); using var staff = await f.As("staff"); using var tenant = await f.As("tenant-a");
        Guid id; using (var s = f.Services.CreateScope()) { var db = s.ServiceProvider.GetRequiredService<CubeDb>(); var r = new ServiceRequest { TenantId = f.TenantA, CategoryId = f.Category, AssigneeId = f.Staff, Subject = "Scoped notification", Description = "Test" }; db.Requests.Add(r); Events.NotifyStaff(db, f.Staff, f.TenantA, "StaffRequestAssigned", r.Id, "Assigned"); await db.SaveChangesAsync(); id = r.Id; }
        (await tenant.PostAsJsonAsync($"/api/v1/requests/{id}/messages", new { Text = "Tenant update" })).EnsureSuccessStatusCode();
        var inbox = await staff.GetFromJsonAsync<JsonElement>("/api/v1/staff/notifications?size=100"); var notice = inbox.EnumerateArray().First(n => n.GetProperty("resourceId").GetGuid() == id); var noticeId = notice.GetProperty("id").GetGuid();
        (await staff.PostAsync($"/api/v1/staff/notifications/{noticeId}/read", null)).EnsureSuccessStatusCode();
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/requests/{id}"); var version = detail.GetProperty("request").GetProperty("version").GetUInt32();
        (await admin.PutAsJsonAsync($"/api/v1/admin/requests/{id}/assignment", new { AssigneeId = (Guid?)null, DueAt = (DateTimeOffset?)null, Version = version })).EnsureSuccessStatusCode();
        Assert.DoesNotContain(id.ToString(), await staff.GetStringAsync("/api/v1/staff/notifications?size=100"));
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsync($"/api/v1/staff/notifications/{noticeId}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/requests/{id}")).StatusCode);
    }
    [Fact]
    public async Task Responsible_employee_can_review_reading_but_cannot_overwrite_review()
    {
        var old = await Route(f.Staff); try
        {
            var id = await Reading(); using var staff = await f.As("staff"); using var admin = await f.As("admin"); using var tenant = await f.As("tenant-a");
            Assert.Equal(HttpStatusCode.Forbidden, (await tenant.PutAsJsonAsync($"/api/v1/staff/readings/{id}/review", new { Status = "Accepted", Reason = "forbidden" })).StatusCode);
            (await staff.PutAsJsonAsync($"/api/v1/staff/readings/{id}/review", new { Status = "Accepted", Reason = "Проверено сотрудником" })).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/admin/readings/{id}/review", new { Status = "Rejected", Reason = "late overwrite" })).StatusCode);
            using var s = f.Services.CreateScope(); var db = s.ServiceProvider.GetRequiredService<CubeDb>(); var r = await db.Readings.FindAsync(id); Assert.Equal("Accepted", r!.Status); Assert.Equal(22, r.Value); Assert.Equal(10, r.PreviousValue);
            await Route(null); Assert.Equal(HttpStatusCode.NotFound, (await staff.PutAsJsonAsync($"/api/v1/staff/readings/{id}/review", new { Status = "Accepted", Reason = "revoked" })).StatusCode);
        }
        finally { await Route(old); }
    }
    [Fact]
    public async Task Concurrent_admin_and_employee_reviews_allow_exactly_one_result()
    {
        var old = await Route(f.Staff); try { var id = await Reading(); using var staff = await f.As("staff"); using var admin = await f.As("admin"); var responses = await Task.WhenAll(staff.PutAsJsonAsync($"/api/v1/staff/readings/{id}/review", new { Status = "Accepted", Reason = "staff" }), admin.PutAsJsonAsync($"/api/v1/admin/readings/{id}/review", new { Status = "Rejected", Reason = "admin" })); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict); } finally { await Route(old); }
    }
    [Fact]
    public async Task Reading_photo_and_notification_follow_current_direction_permission()
    {
        var old = await Route(f.Staff); try
        {
            using var tenant = await f.As("tenant-a"); using var staff = await f.As("staff"); using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0 }), "file", "meter.png"); var upload = await tenant.PostAsync("/api/v1/files", form); upload.EnsureSuccessStatusCode(); var fid = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); var id = await Reading();
            using (var s = f.Services.CreateScope()) { var db = s.ServiceProvider.GetRequiredService<CubeDb>(); var r = (await db.Readings.FindAsync(id))!; r.PhotoId = fid; Events.NotifyReadingStaff(db, (await db.Meters.FindAsync(r.MeterId))!, r); await db.SaveChangesAsync(); }
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/v1/staff/readings/{id}/photo")).StatusCode); Assert.Contains(id.ToString(), await staff.GetStringAsync("/api/v1/staff/notifications?size=100"));
            await Route(null); Assert.Equal(HttpStatusCode.NotFound, (await staff.GetAsync($"/api/v1/staff/readings/{id}/photo")).StatusCode); Assert.DoesNotContain(id.ToString(), await staff.GetStringAsync("/api/v1/staff/notifications?size=100"));
        }
        finally { await Route(old); }
    }

    [Fact]
    public async Task Tenant_attachment_without_text_notifies_assigned_employee()
    {
        using var tenant = await f.As("tenant-a"); using var worker = await f.As("staff"); using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent("%PDF-1.4\nAttachment only"u8.ToArray()), "file", "attachment-only.pdf");
        var uploaded = await tenant.PostAsync("/api/v1/files", form); uploaded.EnsureSuccessStatusCode();
        var id = (await uploaded.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await tenant.PostAsJsonAsync($"/api/v1/requests/{f.RequestA}/attachments", new { FileId = id })).EnsureSuccessStatusCode();
        var inbox = await worker.GetFromJsonAsync<JsonElement>("/api/v1/staff/notifications?size=100");
        Assert.Contains(inbox.EnumerateArray(), n => n.GetProperty("kind").GetString() == "StaffRequestAttachment" && n.GetProperty("resourceId").GetGuid() == f.RequestA);
    }
}
