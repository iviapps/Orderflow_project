using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MockQueryable;
using MockQueryable.Moq;
using Moq;
using Orderflow.Identity.DTOs.Users.Queries;
using Orderflow.Identity.DTOs.Users.Requests;
using Orderflow.Identity.Services.Users;

namespace Orderflow.Identity.Tests.Services;

[TestFixture]
public class UserServiceTests
{
    private Mock<UserManager<IdentityUser>> _userManagerMock;
    private Mock<ILogger<UserService>> _loggerMock;
    private UserService _userService;

    [SetUp]
    public void SetUp()
    {
        var userStoreMock = new Mock<IUserStore<IdentityUser>>();

        _userManagerMock = new Mock<UserManager<IdentityUser>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        _loggerMock = new Mock<ILogger<UserService>>();

        _userService = new UserService(
            _userManagerMock.Object,
            _loggerMock.Object);
    }

    #region GetUsersAsync

    [Test]
    public async Task GetUsersAsync_WhenUsersExist_ReturnsPaginatedUsers()
    {
        // Arrange
        var users = new List<IdentityUser>
        {
            new() { Id = "1", Email = "admin@test.com", UserName = "admin" },
            new() { Id = "2", Email = "user@test.com", UserName = "user" },
            new() { Id = "3", Email = "customer@test.com", UserName = "customer" }
        };

        var mockUsers = users.BuildMock();
        _userManagerMock.Setup(um => um.Users).Returns(mockUsers);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        var parameters = new UserQueryParameters { Page = 1, PageSize = 10 };

        // Act
        var result = await _userService.GetUsersAsync(parameters);

        // Assert
        Assert.That(result.Data.Count(), Is.EqualTo(3));
        Assert.That(result.Pagination.TotalCount, Is.EqualTo(3));
    }

    [Test]
    public async Task GetUsersAsync_WithSearchFilter_ReturnsFilteredUsers()
    {
        // Arrange
        var users = new List<IdentityUser>
        {
            new() { Id = "1", Email = "admin@test.com", UserName = "admin" },
            new() { Id = "2", Email = "user@test.com", UserName = "user" },
            new() { Id = "3", Email = "customer@test.com", UserName = "customer" }
        };

        var mockUsers = users.BuildMock();
        _userManagerMock.Setup(um => um.Users).Returns(mockUsers);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Admin" });

        var parameters = new UserQueryParameters
        {
            Page = 1,
            PageSize = 10,
            Search = "admin"
        };

        // Act
        var result = await _userService.GetUsersAsync(parameters);

        // Assert
        Assert.That(result.Data.Count(), Is.EqualTo(1));
        Assert.That(result.Data.First().Email, Is.EqualTo("admin@test.com"));
    }

    [Test]
    public async Task GetUsersAsync_WithRoleFilter_ReturnsUsersInRole()
    {
        // Arrange
        var users = new List<IdentityUser>
        {
            new() { Id = "1", Email = "admin@test.com", UserName = "admin" },
            new() { Id = "2", Email = "user@test.com", UserName = "user" }
        };

        var mockUsers = users.BuildMock();
        _userManagerMock.Setup(um => um.Users).Returns(mockUsers);

        // Solo el primer usuario está en el rol "Admin"
        _userManagerMock
            .Setup(um => um.GetUsersInRoleAsync("Admin"))
            .ReturnsAsync(new List<IdentityUser> { users[0] });

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Admin" });

        var parameters = new UserQueryParameters
        {
            Page = 1,
            PageSize = 10,
            Role = "Admin"
        };

        // Act
        var result = await _userService.GetUsersAsync(parameters);

        // Assert
        Assert.That(result.Data.Count(), Is.EqualTo(1));
        Assert.That(result.Data.First().UserId, Is.EqualTo("1"));
    }

    [Test]
    public async Task GetUsersAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var users = new List<IdentityUser>
        {
            new() { Id = "1", Email = "a@test.com", UserName = "a" },
            new() { Id = "2", Email = "b@test.com", UserName = "b" },
            new() { Id = "3", Email = "c@test.com", UserName = "c" }
        };

        var mockUsers = users.BuildMock();
        _userManagerMock.Setup(um => um.Users).Returns(mockUsers);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        var parameters = new UserQueryParameters { Page = 2, PageSize = 2 };

        // Act
        var result = await _userService.GetUsersAsync(parameters);

        // Assert
        Assert.That(result.Data.Count(), Is.EqualTo(1)); // Solo 1 usuario en página 2
        Assert.That(result.Pagination.TotalCount, Is.EqualTo(3));
        Assert.That(result.Pagination.TotalPages, Is.EqualTo(2));
    }

    [Test]
    public async Task GetUsersAsync_WithSortDescending_ReturnsSortedUsers()
    {
        // Arrange
        var users = new List<IdentityUser>
        {
            new() { Id = "1", Email = "a@test.com", UserName = "a" },
            new() { Id = "2", Email = "b@test.com", UserName = "b" },
            new() { Id = "3", Email = "c@test.com", UserName = "c" }
        };

        var mockUsers = users.BuildMock();
        _userManagerMock.Setup(um => um.Users).Returns(mockUsers);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        var parameters = new UserQueryParameters
        {
            Page = 1,
            PageSize = 10,
            SortBy = "email",
            SortDescending = true
        };

        // Act
        var result = await _userService.GetUsersAsync(parameters);

        // Assert
        var resultList = result.Data.ToList();
        Assert.That(resultList[0].Email, Is.EqualTo("c@test.com"));
        Assert.That(resultList[2].Email, Is.EqualTo("a@test.com"));
    }

    [Test]
    public async Task GetUsersAsync_WhenNoUsers_ReturnsEmptyResult()
    {
        // Arrange
        var users = new List<IdentityUser>();
        var mockUsers = users.BuildMock();
        _userManagerMock.Setup(um => um.Users).Returns(mockUsers);

        var parameters = new UserQueryParameters { Page = 1, PageSize = 10 };

        // Act
        var result = await _userService.GetUsersAsync(parameters);

        // Assert
        Assert.That(result.Data, Is.Empty);
        Assert.That(result.Pagination.TotalCount, Is.EqualTo(0));
    }

    #endregion

    #region GetUserByIdAsync

    [Test]
    public async Task GetUserByIdAsync_WhenUserExists_ReturnsUserDetail()
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = "1",
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true,
            PhoneNumber = "123456789",
            TwoFactorEnabled = false
        };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Admin", "Customer" });

        // Act
        var result = await _userService.GetUserByIdAsync("1");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data!.UserId, Is.EqualTo("1"));
        Assert.That(result.Data.Email, Is.EqualTo("test@test.com"));
        Assert.That(result.Data.Roles.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetUserByIdAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.GetUserByIdAsync("999");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    #endregion

    #region CreateUserAsync

    [Test]
    public async Task CreateUserAsync_WhenValid_CreatesUserSuccessfully()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "new@test.com",
            UserName = "newuser",
            Password = "Password123!",
            PhoneNumber = "123456789",
            Roles = new[] { "Customer" }
        };

        _userManagerMock
            .Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.FindByNameAsync(request.UserName))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.CreateAsync(It.IsAny<IdentityUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(um => um.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data!.Email, Is.EqualTo("new@test.com"));
        Assert.That(result.Message, Is.EqualTo("User created successfully"));

        _userManagerMock.Verify(
            um => um.CreateAsync(It.IsAny<IdentityUser>(), request.Password),
            Times.Once);
    }

    [Test]
    public async Task CreateUserAsync_WhenEmailExists_ReturnsFailure()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "existing@test.com",
            Password = "Password123!"
        };

        _userManagerMock
            .Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync(new IdentityUser { Id = "1", Email = request.Email });

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("A user with this email already exists"));

        _userManagerMock.Verify(
            um => um.CreateAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Test]
    public async Task CreateUserAsync_WhenUsernameExists_ReturnsFailure()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "new@test.com",
            UserName = "existinguser",
            Password = "Password123!"
        };

        _userManagerMock
            .Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.FindByNameAsync(request.UserName))
            .ReturnsAsync(new IdentityUser { Id = "1", UserName = request.UserName });

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("A user with this username already exists"));
    }

    [Test]
    public async Task CreateUserAsync_WhenCreateFails_ReturnsFailure()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "new@test.com",
            Password = "weak"
        };

        _userManagerMock
            .Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.CreateAsync(It.IsAny<IdentityUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Code = "PasswordTooShort", Description = "Password too short" }));

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        Assert.That(result.Succeeded, Is.False);
    }

    [Test]
    public async Task CreateUserAsync_WithNoRoles_AssignsDefaultCustomerRole()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "new@test.com",
            Password = "Password123!",
            Roles = null // Sin roles especificados
        };

        _userManagerMock
            .Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.CreateAsync(It.IsAny<IdentityUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(um => um.AddToRoleAsync(It.IsAny<IdentityUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        // Act
        var result = await _userService.CreateUserAsync(request);

        // Assert
        Assert.That(result.Succeeded, Is.True);

        // Verificar que se asignó el rol Customer por defecto
        _userManagerMock.Verify(
            um => um.AddToRoleAsync(It.IsAny<IdentityUser>(), "Customer"),
            Times.Once);
    }

    #endregion

    #region UpdateUserAsync

    [Test]
    public async Task UpdateUserAsync_WhenValid_UpdatesUserSuccessfully()
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = "1",
            Email = "old@test.com",
            UserName = "olduser"
        };

        var request = new UpdateUserRequest
        {
            Email = "new@test.com",
            UserName = "newuser",
            PhoneNumber = "987654321",
            EmailConfirmed = true,
            LockoutEnabled = false
        };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.FindByEmailAsync(request.Email))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.FindByNameAsync(request.UserName))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.UpdateAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        // Act
        var result = await _userService.UpdateUserAsync("1", request);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data!.Email, Is.EqualTo("new@test.com"));
        Assert.That(result.Message, Is.EqualTo("User updated successfully"));
    }

    [Test]
    public async Task UpdateUserAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        var request = new UpdateUserRequest { Email = "test@test.com", UserName = "test" };

        // Act
        var result = await _userService.UpdateUserAsync("999", request);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    [Test]
    public async Task UpdateUserAsync_WhenEmailTakenByOther_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1", Email = "old@test.com", UserName = "user1" };
        var otherUser = new IdentityUser { Id = "2", Email = "taken@test.com" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.FindByEmailAsync("taken@test.com"))
            .ReturnsAsync(otherUser);

        var request = new UpdateUserRequest { Email = "taken@test.com", UserName = "user1" };

        // Act
        var result = await _userService.UpdateUserAsync("1", request);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("Email is already taken by another user"));
    }

    [Test]
    public async Task UpdateUserAsync_WhenUsernameTakenByOther_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1", Email = "test@test.com", UserName = "oldname" };
        var otherUser = new IdentityUser { Id = "2", UserName = "takenname" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.FindByNameAsync("takenname"))
            .ReturnsAsync(otherUser);

        var request = new UpdateUserRequest { Email = "test@test.com", UserName = "takenname" };

        // Act
        var result = await _userService.UpdateUserAsync("1", request);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("Username is already taken by another user"));
    }

    #endregion

    #region DeleteUserAsync

    [Test]
    public async Task DeleteUserAsync_WhenUserExists_DeletesSuccessfully()
    {
        // Arrange
        var user = new IdentityUser { Id = "1", Email = "test@test.com" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userService.DeleteUserAsync("1");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Is.EqualTo("User deleted successfully"));

        _userManagerMock.Verify(um => um.DeleteAsync(user), Times.Once);
    }

    [Test]
    public async Task DeleteUserAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.DeleteUserAsync("999");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    [Test]
    public async Task DeleteUserAsync_WhenDeleteFails_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.DeleteAsync(user))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "Database error" }));

        // Act
        var result = await _userService.DeleteUserAsync("1");

        // Assert
        Assert.That(result.Succeeded, Is.False);
    }

    #endregion

    #region LockUserAsync

    [Test]
    public async Task LockUserAsync_WhenUserExists_LocksSuccessfully()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };
        var lockoutEnd = DateTimeOffset.UtcNow.AddDays(7);

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.SetLockoutEndDateAsync(user, lockoutEnd))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userService.LockUserAsync("1", lockoutEnd);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Is.EqualTo("User account locked successfully"));
    }

    [Test]
    public async Task LockUserAsync_WithoutEndDate_LocksPermanently()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.SetLockoutEndDateAsync(user, It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userService.LockUserAsync("1", null);

        // Assert
        Assert.That(result.Succeeded, Is.True);

        // Verificar que se usó una fecha muy lejana (100 años)
        _userManagerMock.Verify(
            um => um.SetLockoutEndDateAsync(
                user,
                It.Is<DateTimeOffset>(d => d > DateTimeOffset.UtcNow.AddYears(99))),
            Times.Once);
    }

    [Test]
    public async Task LockUserAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.LockUserAsync("999", null);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    #endregion

    #region UnlockUserAsync

    [Test]
    public async Task UnlockUserAsync_WhenUserExists_UnlocksSuccessfully()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.SetLockoutEndDateAsync(user, null))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(um => um.ResetAccessFailedCountAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userService.UnlockUserAsync("1");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Is.EqualTo("User account unlocked successfully"));

        _userManagerMock.Verify(um => um.SetLockoutEndDateAsync(user, null), Times.Once);
        _userManagerMock.Verify(um => um.ResetAccessFailedCountAsync(user), Times.Once);
    }

    [Test]
    public async Task UnlockUserAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.UnlockUserAsync("999");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    #endregion

    #region ChangePasswordAsync

    [Test]
    public async Task ChangePasswordAsync_WhenValid_ChangesPasswordSuccessfully()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.ChangePasswordAsync(user, "oldPassword", "newPassword"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userService.ChangePasswordAsync("1", "oldPassword", "newPassword");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Is.EqualTo("Password changed successfully"));
    }

    [Test]
    public async Task ChangePasswordAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.ChangePasswordAsync("999", "old", "new");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    [Test]
    public async Task ChangePasswordAsync_WhenCurrentPasswordWrong_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.ChangePasswordAsync(user, "wrongPassword", "newPassword"))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "Incorrect password" }));

        // Act
        var result = await _userService.ChangePasswordAsync("1", "wrongPassword", "newPassword");

        // Assert
        Assert.That(result.Succeeded, Is.False);
    }

    #endregion

    #region GetUserRolesAsync

    [Test]
    public async Task GetUserRolesAsync_WhenUserExists_ReturnsRoles()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Admin", "Customer" });

        // Act
        var result = await _userService.GetUserRolesAsync("1");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data!.ToList(), Has.Count.EqualTo(2));

        Assert.That(result.Data, Does.Contain("Admin"));
        Assert.That(result.Data, Does.Contain("Customer"));
    }

    [Test]
    public async Task GetUserRolesAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.GetUserRolesAsync("999");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    #endregion

    #region AddUserToRoleAsync

    [Test]
    public async Task AddUserToRoleAsync_WhenValid_AddsRoleSuccessfully()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Customer" }); // No tiene Admin

        _userManagerMock
            .Setup(um => um.AddToRoleAsync(user, "Admin"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userService.AddUserToRoleAsync("1", "Admin");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Is.EqualTo("Role 'Admin' assigned successfully"));
    }

    [Test]
    public async Task AddUserToRoleAsync_WhenUserAlreadyHasRole_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Admin" }); // Ya tiene Admin

        // Act
        var result = await _userService.AddUserToRoleAsync("1", "Admin");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User already has this role"));
    }

    [Test]
    public async Task AddUserToRoleAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.AddUserToRoleAsync("999", "Admin");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    #endregion

    #region RemoveUserFromRoleAsync

    [Test]
    public async Task RemoveUserFromRoleAsync_WhenValid_RemovesRoleSuccessfully()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.IsInRoleAsync(user, "Admin"))
            .ReturnsAsync(true);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Admin", "Customer" }); // Tiene 2 roles

        _userManagerMock
            .Setup(um => um.RemoveFromRoleAsync(user, "Admin"))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _userService.RemoveUserFromRoleAsync("1", "Admin");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Is.EqualTo("Role 'Admin' removed successfully"));
    }

    [Test]
    public async Task RemoveUserFromRoleAsync_WhenUserDoesNotHaveRole_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.IsInRoleAsync(user, "Admin"))
            .ReturnsAsync(false);

        // Act
        var result = await _userService.RemoveUserFromRoleAsync("1", "Admin");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User does not have this role"));
    }

    [Test]
    public async Task RemoveUserFromRoleAsync_WhenLastRole_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.IsInRoleAsync(user, "Customer"))
            .ReturnsAsync(true);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Customer" }); // Solo tiene 1 rol

        // Act
        var result = await _userService.RemoveUserFromRoleAsync("1", "Customer");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Does.Contain("Cannot remove the last role"));
    }

    [Test]
    public async Task RemoveUserFromRoleAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.RemoveUserFromRoleAsync("999", "Admin");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    #endregion

    #region GetCurrentUserProfileAsync

    [Test]
    public async Task GetCurrentUserProfileAsync_WhenUserExists_ReturnsProfile()
    {
        // Arrange
        var user = new IdentityUser
        {
            Id = "1",
            Email = "test@test.com",
            UserName = "testuser",
            EmailConfirmed = true,
            PhoneNumber = "123456789",
            TwoFactorEnabled = false
        };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "Customer" });

        // Act
        var result = await _userService.GetCurrentUserProfileAsync("1");

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.Not.Null);
        Assert.That(result.Data!.UserId, Is.EqualTo("1"));
        Assert.That(result.Data.Email, Is.EqualTo("test@test.com"));
        Assert.That(result.Data.UserName, Is.EqualTo("testuser"));
        Assert.That(result.Data.PhoneNumber, Is.EqualTo("123456789"));
        Assert.That(result.Data.Roles, Does.Contain("Customer"));
    }

    [Test]
    public async Task GetCurrentUserProfileAsync_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        _userManagerMock
            .Setup(um => um.FindByIdAsync("999"))
            .ReturnsAsync((IdentityUser?)null);

        // Act
        var result = await _userService.GetCurrentUserProfileAsync("999");

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Errors!.First(), Is.EqualTo("User not found"));
    }

    #endregion

    #region UpdateCurrentUserProfileAsync

    [Test]
    public async Task UpdateCurrentUserProfileAsync_WhenValid_UpdatesSuccessfully()
    {
        // Arrange
        var user = new IdentityUser { Id = "1", UserName = "oldname", Email = "test@test.com" };

        var request = new UpdateProfileRequest
        {
            UserName = "newname",
            PhoneNumber = "123456789"
        };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.FindByNameAsync("newname"))
            .ReturnsAsync((IdentityUser?)null);

        _userManagerMock
            .Setup(um => um.UpdateAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        // Act
        var result = await _userService.UpdateCurrentUserProfileAsync("1", request);

        // Assert
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Is.EqualTo("Profile updated successfully"));
    }

    [Test]
    public async Task UpdateCurrentUserProfileAsync_WhenUsernameTaken_ReturnsFailure()
    {
        // Arrange
        var user = new IdentityUser { Id = "1", UserName = "oldname" };
        var otherUser = new IdentityUser { Id = "2", UserName = "takenname" };

        var request = new UpdateProfileRequest { UserName = "takenname" };

        _userManagerMock
            .Setup(um => um.FindByIdAsync("1"))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(um => um.FindByNameAsync("takenname"))
            .ReturnsAsync(otherUser);

        // Act
        var result = await _userService.UpdateCurrentUserProfileAsync("1", request);

        // Assert
        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors!.First(), Is.EqualTo("Username is already taken"));
    }

    #endregion
}