using ResetYourFuture.Application.DTOs;
using Shouldly;
using Xunit;

namespace ResetYourFuture.Application.Tests;

public class PagedResultTests
{
    private static PagedResult<int> Page(int total, int page, int pageSize) =>
        new([], total, page, pageSize);

    [Theory]
    [InlineData(10, 3, 4)]  // ceil(10/3)
    [InlineData(9, 3, 3)]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    public void TotalPages_IsCeilingOfTotalOverPageSize(int total, int pageSize, int expected)
    {
        Page(total, 1, pageSize).TotalPages.ShouldBe(expected);
    }

    [Fact]
    public void TotalPages_ZeroPageSize_DoesNotDivideByZero()
    {
        Page(10, 1, 0).TotalPages.ShouldBe(0);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void HasPreviousPage_TrueAfterFirstPage(int page, bool expected)
    {
        Page(100, page, 10).HasPreviousPage.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, true)]   // 3 pages total
    [InlineData(2, true)]
    [InlineData(3, false)]  // last page
    public void HasNextPage_FalseOnLastPage(int page, bool expected)
    {
        Page(25, page, 10).HasNextPage.ShouldBe(expected);
    }
}
