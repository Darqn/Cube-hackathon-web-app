using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cube.Api;
using Cube.Api.Modules.Metering.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Cube.Tests;

public sealed class MeterOcrEngineTests
{
    private static string Example => Path.Combine(AppContext.BaseDirectory, "Fixtures", "meter-example.png");
    private static MeterOcr Engine(string? executable = null) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Ocr:TesseractPath"] = executable }).Build());

    [Fact]
    public async Task Real_tesseract_recognizes_the_branch_example()
    {
        await using var image = File.OpenRead(Example);
        Assert.Equal("12480", await Engine().Recognize(image, image.Length, "image/png", CancellationToken.None));
    }

    [Fact]
    public async Task Missing_optional_engine_allows_manual_entry()
    {
        await using var image = File.OpenRead(Example);
        Assert.Null(await Engine(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing-tesseract"))
            .Recognize(image, image.Length, "image/png", CancellationToken.None));
    }

    [Theory]
    [InlineData(0, "image/png")]
    [InlineData(MeterOcr.MaximumFileSize + 1, "image/png")]
    [InlineData(20, "application/pdf")]
    [InlineData(20, "image/jpeg")]
    [InlineData(20, "image/png")]
    public async Task Invalid_uploads_do_not_produce_readings(long length, string contentType)
    {
        await using var image = new MemoryStream("not a valid image"u8.ToArray());
        Assert.Null(await Engine().Recognize(image, length, contentType, CancellationToken.None));
    }

    [LinuxFact]
    public async Task Request_cancellation_terminates_the_native_process_and_removes_its_image()
    {
        var directory = Path.Combine(Path.GetTempPath(), "cube-ocr-process-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Combine(directory, "waiting-engine");
        var marker = executable + ".started";
        Process? native = null;
        using var cancellation = new CancellationTokenSource();
        Task<string?>? recognition = null;
        try
        {
            // A controlled long-running native process exercises cancellation, not recognition.
            await File.WriteAllTextAsync(executable, "#!/bin/sh\nprintf '%s\\n%s\\n' \"$$\" \"$1\" > \"$0.started\"\nexec sleep 60\n");
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await using var image = File.OpenRead(Example);
            recognition = Engine(executable).Recognize(image, image.Length, "image/png", cancellation.Token);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string[] state;
            do
            {
                await Task.Delay(20, deadline.Token);
                state = File.Exists(marker) ? await File.ReadAllLinesAsync(marker, deadline.Token) : [];
            } while (state.Length < 2);
            native = Process.GetProcessById(int.Parse(state[0]));
            Assert.True(File.Exists(state[1]));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recognition.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(native.HasExited);
            Assert.False(File.Exists(state[1]));
        }
        finally
        {
            cancellation.Cancel();
            if (recognition is not null)
            {
                try { await recognition.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
            if (native is not null)
            {
                if (!native.HasExited) native.Kill(entireProcessTree: true);
                native.Dispose();
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class LinuxFactAttribute : FactAttribute
    {
        public LinuxFactAttribute()
        {
            if (!OperatingSystem.IsLinux()) Skip = "This native process lifecycle test uses a POSIX shell.";
        }
    }
}

public sealed class MeterOcrHttpTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Route = "/api/v1/meters/recognize";

    [Fact]
    public async Task Recognition_requires_authentication()
    {
        using var client = fixture.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(Route, null)).StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task Exactly_one_multipart_file_is_required(int count)
    {
        using var client = await fixture.As("tenant-a");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("No reading is submitted by recognition"), "description");
        for (var index = 0; index < count; index++) form.Add(new ByteArrayContent([1, 2, 3]), "file", $"{index}.png");
        var response = await client.PostAsync(Route, form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(MeterOcrEndpoints.Failure, body.GetProperty("message").GetString());
        Assert.Single(body.EnumerateObject());
    }

    [Fact]
    public async Task Non_form_input_keeps_the_ocr_error_contract()
    {
        using var client = await fixture.As("tenant-a");
        var response = await client.PostAsJsonAsync(Route, new { value = "12480" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(MeterOcrEndpoints.Failure, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("image/png", 20)]
    [InlineData("image/png", MeterOcr.MaximumFileSize + 1)]
    [InlineData("application/pdf", 20)]
    public async Task Unreadable_or_unsupported_images_keep_manual_fallback(string contentType, int length)
    {
        using var client = await fixture.As("tenant-a");
        using var form = Upload(new byte[length], contentType);
        var response = await client.PostAsync(Route, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("value").ValueKind);
        Assert.Equal(MeterOcrEndpoints.Failure, body.GetProperty("message").GetString());
        Assert.Equal(2, body.EnumerateObject().Count());
    }

    [Fact]
    public async Task Real_http_recognition_returns_a_suggestion_without_saving_a_reading_or_file()
    {
        using var client = await fixture.As("tenant-a");
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CubeDb>();
        var readingsBefore = await db.Readings.CountAsync();
        var filesBefore = await db.Files.CountAsync();
        var meterBefore = await db.Meters.AsNoTracking().SingleAsync(x => x.Id == fixture.MeterA);
        using var form = Upload(await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "meter-example.png")), "image/png");
        var response = await client.PostAsync(Route, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("12480", body.GetProperty("value").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("message").ValueKind);
        Assert.Equal(2, body.EnumerateObject().Count());
        Assert.Equal(readingsBefore, await db.Readings.CountAsync());
        Assert.Equal(filesBefore, await db.Files.CountAsync());
        Assert.Equal(meterBefore.LastValue, await db.Meters.Where(x => x.Id == fixture.MeterA).Select(x => x.LastValue).SingleAsync());
    }

    private static MultipartFormDataContent Upload(byte[] bytes, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", "meter.png");
        return form;
    }
}
