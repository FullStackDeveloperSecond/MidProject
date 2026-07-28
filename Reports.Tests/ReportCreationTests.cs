using MidProject.Models.DTOs;
using Xunit;

namespace Reports.Tests;

public sealed class ReportCreationTests : ReportTestBase
{
    [Fact]
    public async Task CreateReport_OwnContent_IsRejected()
    {
        var dto = new ReportCreateDto
        {
            ReviewID = ReviewId,
            Category = "人身攻擊",
            Reason = "測試原因"
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Service.CreateReportAsync(dto, OwnerId));

        Assert.Equal("不能檢舉自己的內容。", exception.Message);
        Assert.Empty(Db.Reports);
    }
}
