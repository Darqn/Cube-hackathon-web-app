using Cube.Api.Modules.Metering.Domain;
using Xunit;

namespace Cube.Tests;

public sealed class MeterOcrTests
{
    private const string Header = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n";
    private static string Row(int line, double confidence, string value) => $"5\t1\t1\t1\t{line}\t1\t0\t0\t10\t10\t{confidence.ToString(System.Globalization.CultureInfo.InvariantCulture)}\t{value}\n";
    [Fact] public void Single_clear_integer_is_accepted() => Assert.Equal("12480", IntegerReadingRecognition.ParseTsv(Header + Row(1, 95, "12480")));
    [Fact] public void Low_confidence_is_rejected() => Assert.Null(IntegerReadingRecognition.ParseTsv(Header + Row(1, 40, "12480")));
    [Fact] public void Multiple_numbers_are_rejected() => Assert.Null(IntegerReadingRecognition.ParseTsv(Header + Row(1, 95, "12480") + Row(2, 95, "7845")));
    [Fact] public void Decimal_is_rejected() => Assert.Null(IntegerReadingRecognition.ParseTsv(Header + Row(1, 95, "12480.5")));
}
