using System.Globalization;
using System.Text.RegularExpressions;

namespace Cube.Api.Modules.Metering.Domain;

// First version: one prominent integer reading, no multi-tariff or decimal display.
public static class IntegerReadingRecognition
{
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
