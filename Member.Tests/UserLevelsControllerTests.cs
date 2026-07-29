using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MidProject.Models;
using MidProject.Models.ViewModels;

namespace Member.Tests;

public sealed class UserLevelsControllerTests
{
    [Fact]
    public async Task Create_MapsAllowedFields_AndCannotCreateDeletedLevel()
    {
        await using var context = InMemoryDbContextFactory.Create();
        var controller = new UserLevelsController(context);
        var form = new UserLevelFormViewModel
        {
            LevelName = "  黃金  ",
            MinExp = 100,
            Rewards = "  徽章  "
        };

        var result = await controller.Create(form);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = await context.UserLevels.SingleAsync();
        Assert.Equal("黃金", saved.LevelName);
        Assert.Equal(100, saved.MinExp);
        Assert.Equal("徽章", saved.Rewards);
        Assert.False(saved.IsDeleted);
    }

    [Fact]
    public async Task Create_DuplicateNameOrMinimumExperience_ReturnsValidationErrors()
    {
        await using var context = InMemoryDbContextFactory.Create();
        context.UserLevels.AddRange(
            new UserLevel { LevelName = "黃金", MinExp = 100 },
            new UserLevel { LevelName = "白金", MinExp = 200 });
        await context.SaveChangesAsync();
        var controller = new UserLevelsController(context);

        var result = await controller.Create(new UserLevelFormViewModel
        {
            LevelName = "黃金",
            MinExp = 200
        });

        Assert.IsType<ViewResult>(result);
        Assert.Contains(nameof(UserLevelFormViewModel.LevelName), controller.ModelState.Keys);
        Assert.Contains(nameof(UserLevelFormViewModel.MinExp), controller.ModelState.Keys);
        Assert.Equal(2, await context.UserLevels.CountAsync());
    }

    [Fact]
    public void FormModel_RejectsNegativeMinimumExperience()
    {
        var form = new UserLevelFormViewModel
        {
            LevelName = "測試",
            MinExp = -1
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            form,
            new ValidationContext(form),
            results,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(UserLevelFormViewModel.MinExp)));
    }
}
