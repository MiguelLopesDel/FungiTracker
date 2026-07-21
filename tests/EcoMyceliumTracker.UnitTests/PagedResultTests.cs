using EcoMyceliumTracker.Models;

namespace EcoMyceliumTracker.UnitTests;

public sealed class PagedResultTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    // A partial last page still counts as a page, which is where a floor
    // division would silently drop records from the total.
    [InlineData(21, 20, 2)]
    [InlineData(100, 7, 15)]
    public void TotalPages_RoundsUpAndHandlesAnEmptyResult(
        long totalItems,
        int pageSize,
        int expectedPages)
    {
        var result = new PagedResult<string>([], 1, pageSize, totalItems);

        Assert.Equal(expectedPages, result.TotalPages);
    }
}
