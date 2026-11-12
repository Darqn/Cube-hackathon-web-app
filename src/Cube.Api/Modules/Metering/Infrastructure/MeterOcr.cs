using System.ComponentModel;
using System.Diagnostics;
using Cube.Api.Modules.Metering.Domain;

namespace Cube.Api.Modules.Metering.Infrastructure;

// Tesseract is optional: unreadable images or a missing executable allow manual entry.
public sealed class MeterOcr(IConfiguration configuration)
{
    public const int MaximumFileSize = 5 * 1024 * 1024;

    public async Task<string?> Recognize(Stream image, long length, string contentType, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (length is <= 0 or > MaximumFileSize || contentType is not ("image/jpeg" or "image/png")) return null;

        var extension = contentType == "image/png" ? ".png" : ".jpg";
        var path = Path.Combine(Path.GetTempPath(), $"cube-ocr-{Guid.NewGuid():N}{extension}");
        try
        {
            await using (var stream = File.Create(path)) await image.CopyToAsync(stream, ct);
            var signature = new byte[8];
            await using (var stream = File.OpenRead(path))
            {
                if (await stream.ReadAtLeastAsync(signature, signature.Length, throwOnEndOfStream: false, ct) < signature.Length) return null;
            }
            var png = signature.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var jpeg = signature[0] == 0xFF && signature[1] == 0xD8 && signature[2] == 0xFF;
            if (extension == ".png" ? !png : !jpeg) return null;

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = configuration["Ocr:TesseractPath"] ?? "tesseract",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            // Retain punctuation so a decimal display cannot silently become an integer.
            foreach (var arg in new[] { path, "stdout", "--psm", "6", "-c", "tessedit_char_whitelist=0123456789.,", "tsv" })
                process.StartInfo.ArgumentList.Add(arg);
            ct.ThrowIfCancellationRequested();
            try { process.Start(); }
            catch (Win32Exception) { return null; }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(12));
            // Drain both pipes, including after cancellation, before disposing the process.
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var tsv = await output;
                _ = await errors;
                return process.ExitCode == 0 ? IntegerReadingRecognition.ParseTsv(tsv) : null;
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { /* The process exited between cancellation and Kill. */ }
                await process.WaitForExitAsync(CancellationToken.None);
                await Task.WhenAll(output, errors);
                ct.ThrowIfCancellationRequested();
                return null;
            }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
