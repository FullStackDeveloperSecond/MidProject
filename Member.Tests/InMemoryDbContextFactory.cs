using Microsoft.EntityFrameworkCore;
using MidProject.Data;

namespace Member.Tests;

public static class InMemoryDbContextFactory
{
    // 每個測試都用一個獨立命名的 InMemory 資料庫，測試之間不會互相汙染資料。
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
