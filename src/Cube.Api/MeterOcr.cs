using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Cube.Api;

// First version: one prominent INTEGER reading, no multi-tariff or decimal display.
public sealed class MeterOcr(IConfiguration configuration)
{
    public const string Failure = "Не удалось уверенно распознать. Введите показание вручную";

    public async Task<string?> Recognize(IFormFile file, CancellationToken ct)
    {
        if (file.Length is <= 0 or > 5 * 1024 * 1024) return null;
        if (file.ContentType is not ("image/jpeg" or "image/png")) return null;

        var extension = file.ContentType == "image/png" ? ".png" : ".jpg";
        var path = Path.Combine(Path.GetTempPath(), $"cube-ocr-{Guid.NewGuid():N}{extension}");
        try
        {
            await using (var stream = File.Create(path)) await file.CopyToAsync(stream, ct);
            var signature = new byte[8];
            await using (var stream = File.OpenRead(path))
            {
                if (await stream.ReadAsync(signature, ct) < 8) return null;
            }
            bool png = signature.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            bool jpeg = signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF;
            if (extension == ".png" ? !png : !jpeg) return null;

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = configuration["Ocr:TesseractPath"] ?? "tesseract",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[] { path, "stdout", "--psm", "6", "-c", "tessedit_char_whitelist=0123456789", "tsv" })
                process.StartInfo.ArgumentList.Add(arg);
            try { process.Start(); }
            catch (System.ComponentModel.Win32Exception) { return null; } // not installed
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            try
            {
                var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                var errors = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                var tsv = await output;
                _ = await errors;
                return process.ExitCode == 0 ? ParseTsv(tsv) : null;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                return null;
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    // Read only OCR word rows. Ambiguous or multiple numeric groups are rejected.
    public static string? ParseTsv(string tsv)
    {
        var candidates = new Dictionary<string, List<(string Text, double Confidence)>>();
        foreach (var line in tsv.Split('\n').Skip(1))
        {
            var fields = line.TrimEnd('\r').Split('\t', 12);
            if (fields.Length != 12 || fields[0] != "5") continue;
            var text = fields[11].Trim();
            if (text.Length == 0) continue;
            if (!double.TryParse(fields[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence)) continue;
            var lineKey = string.Join(':', fields[1], fields[2], fields[3], fields[4]);
            if (!candidates.TryGetValue(lineKey, out var words)) candidates[lineKey] = words = [];
            words.Add((text, confidence));
        }
        var readings = candidates.Values.Select(words => new
        {
            Text = string.Concat(words.Select(w => w.Text)),
            Confidence = words.Average(w => w.Confidence)
        }).Where(x => x.Confidence >= 80 && Regex.IsMatch(x.Text, @"^[0-9]{1,12}$"))
          .Select(x => x.Text).Distinct().ToList();
        return readings.Count == 1 ? readings[0] : null;
    }
}

public static class MeterOcrEndpoints
{
    public static void MapMeterOcr(this WebApplication app)
    {
        app.MapPost("/api/v1/meters/recognize", async (HttpRequest request, MeterOcr ocr, CancellationToken ct) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { message = MeterOcr.Failure });
            var form = await request.ReadFormAsync(ct);
            if (form.Files.Count != 1) return Results.BadRequest(new { message = MeterOcr.Failure });
            var value = await ocr.Recognize(form.Files[0], ct);
            return Results.Ok(new { value, message = value is null ? MeterOcr.Failure : (string?)null });
        }).RequireAuthorization().WithTags("Meter OCR");
    }
}
