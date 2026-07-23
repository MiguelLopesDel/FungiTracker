using EcoMyceliumTracker.Domain;

namespace EcoMyceliumTracker.UnitTests;

public sealed class CoordinatesParserTests
{
    [Theory]
    [InlineData("-23.5505,-46.6333", -23.5505, -46.6333)]
    [InlineData("(10.5, 20.25)", 10.5, 20.25)]
    [InlineData("0,0", 0, 0)]
    public void TryParse_WithValidLocation_ReturnsCoordinates(
        string location,
        double expectedX,
        double expectedY)
    {
        var parsed = CoordinatesParser.TryParse(location, out var coordinates);

        Assert.True(parsed);
        Assert.Equal(expectedX, coordinates.X);
        Assert.Equal(expectedY, coordinates.Y);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10")]
    [InlineData("10,20,30")]
    [InlineData("10.5;20.5")]
    [InlineData("NaN,10")]
    [InlineData("Infinity,10")]
    // Parentheses are only stripped as a matched pair; a lone one is not a
    // delimiter and must not be trimmed off the number.
    [InlineData("(10,20")]
    [InlineData("10,20)")]
    [InlineData("()")]
    public void TryParse_WithInvalidLocation_ReturnsFalse(string? location)
    {
        Assert.False(CoordinatesParser.TryParse(location, out _));
    }
}
