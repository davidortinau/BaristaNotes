using Moq;
using Xunit;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Tests.Unit.Services;

public class UserProfileContextTests
{
    [Fact]
    public async Task CreateProfile_WithContext_PersistsContext()
    {
        var (sut, repo) = CreateSut();
        UserProfile? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<UserProfile>()))
            .Callback<UserProfile>(p => { p.Id = 1; captured = p; })
            .ReturnsAsync((UserProfile p) => p);

        var result = await sut.CreateProfileAsync(new CreateUserProfileDto
        {
            Name = "David",
            Context = "Likes single-origin pour overs in the morning."
        });

        Assert.Equal("Likes single-origin pour overs in the morning.", captured!.Context);
        Assert.Equal("Likes single-origin pour overs in the morning.", result.Context);
    }

    [Fact]
    public async Task UpdateProfile_WithContext_PersistsContext()
    {
        var existing = new UserProfile { Id = 1, Name = "David", Context = "old" };
        var (sut, repo) = CreateSut();
        repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAsync(It.IsAny<UserProfile>())).ReturnsAsync((UserProfile p) => p);

        var result = await sut.UpdateProfileAsync(1, new UpdateUserProfileDto
        {
            Context = "Now prefers espresso after 3pm."
        });

        Assert.Equal("Now prefers espresso after 3pm.", existing.Context);
        Assert.Equal("Now prefers espresso after 3pm.", result.Context);
    }

    [Fact]
    public async Task CreateProfile_ContextTooLong_Throws()
    {
        var (sut, _) = CreateSut();
        var long2001 = new string('x', 2001);

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.CreateProfileAsync(new CreateUserProfileDto { Name = "X", Context = long2001 }));
    }

    [Fact]
    public async Task UpdateProfile_ContextTooLong_Throws()
    {
        var existing = new UserProfile { Id = 1, Name = "David" };
        var (sut, repo) = CreateSut();
        repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);

        await Assert.ThrowsAsync<ValidationException>(() =>
            sut.UpdateProfileAsync(1, new UpdateUserProfileDto { Context = new string('y', 2001) }));
    }

    [Fact]
    public async Task UpdateProfile_NullContext_LeavesExistingContext()
    {
        var existing = new UserProfile { Id = 1, Name = "David", Context = "keep me" };
        var (sut, repo) = CreateSut();
        repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);
        repo.Setup(r => r.UpdateAsync(It.IsAny<UserProfile>())).ReturnsAsync((UserProfile p) => p);

        await sut.UpdateProfileAsync(1, new UpdateUserProfileDto { Name = "David2" });

        Assert.Equal("keep me", existing.Context);
    }

    private static (IUserProfileService sut, Mock<IUserProfileRepository> repo) CreateSut()
    {
        var repo = new Mock<IUserProfileRepository>();
        var img = new Mock<IImageProcessingService>();
        return (new UserProfileService(repo.Object, img.Object), repo);
    }
}
