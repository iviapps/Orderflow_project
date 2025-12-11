using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Orderflow.Shared.Common;
using OrderFlow.Identity.Services.Roles;
using MockQueryable; 
using MockQueryable.Moq;

namespace Orderflow.Identity.Tests.Services
{
    [TestFixture]
    public class RoleServiceTests
    {
        private Mock<RoleManager<IdentityRole>> _roleManagerMock;
        private Mock<UserManager<IdentityUser>> _userManagerMock;
        private Mock<ILogger<RoleService>> _loggerMock;
        private RoleService _roleService;

        [SetUp]
        public void SetUp()
        {
            // Mock de IRoleStore para RoleManager
            var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();

            _roleManagerMock = new Mock<RoleManager<IdentityRole>>(
                roleStoreMock.Object,
                null!, null!, null!, null!);

            // Mock de IUserStore para UserManager
            var userStoreMock = new Mock<IUserStore<IdentityUser>>();

            _userManagerMock = new Mock<UserManager<IdentityUser>>(
                userStoreMock.Object,
                null!, null!, null!, null!, null!, null!, null!, null!);

            _loggerMock = new Mock<ILogger<RoleService>>();

            _roleService = new RoleService(
                _roleManagerMock.Object,
                _userManagerMock.Object,
                _loggerMock.Object);
        }

        #region GetAllRolesAsync

        [Test]
        public async Task GetAllRolesAsync_WhenRolesExist_ReturnsRolesWithUserCounts()
        {
            // Arrange
            var roles = new List<IdentityRole>
            {
                new IdentityRole { Id = "1", Name = "Admin", NormalizedName = "ADMIN" },
                new IdentityRole { Id = "2", Name = "Customer", NormalizedName = "CUSTOMER" }
            };

            // ✅ CORRECCIÓN: Usar BuildMock() en lugar de AsQueryable()
            // Esto permite que operaciones async como ToListAsync() funcionen correctamente
            // ✅ Versión 10.x
            var mockRoles = roles.BuildMock();
            _roleManagerMock
                .Setup(rm => rm.Roles)
                .Returns(mockRoles);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<IdentityUser>
                {
                    new IdentityUser { Id = "U1", UserName = "admin1" }
                });

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Customer"))
                .ReturnsAsync(new List<IdentityUser>
                {
                    new IdentityUser { Id = "U2", UserName = "customer1" },
                    new IdentityUser { Id = "U3", UserName = "customer2" }
                });

            // Act
            var result = await _roleService.GetAllRolesAsync();

            // Assert
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Data, Is.Not.Null);

            var list = result.Data!.ToList();
            Assert.That(list, Has.Count.EqualTo(2));

            var admin = list.Single(r => r.RoleName == "Admin");
            var customer = list.Single(r => r.RoleName == "Customer");

            Assert.That(admin.UserCount, Is.EqualTo(1));
            Assert.That(customer.UserCount, Is.EqualTo(2));
        }

        [Test]
        public async Task GetAllRolesAsync_WhenNoRolesExist_ReturnsEmptyList()
        {
            // Arrange
            var roles = new List<IdentityRole>();
            var mockRoles = roles.BuildMock();

            _roleManagerMock
                .Setup(rm => rm.Roles)
                .Returns(mockRoles);

            // Act
            var result = await _roleService.GetAllRolesAsync();

            // Assert
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data, Is.Empty);
        }

        #endregion

        #region GetRoleByIdAsync

        [Test]
        public async Task GetRoleByIdAsync_WhenRoleExists_ReturnsRoleDetail()
        {
            // Arrange
            var role = new IdentityRole
            {
                Id = "1",
                Name = "Admin",
                NormalizedName = "ADMIN"
            };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<IdentityUser>
                {
                    new IdentityUser { Id = "U1" },
                    new IdentityUser { Id = "U2" }
                });

            // Act
            var result = await _roleService.GetRoleByIdAsync("1");

            // Assert
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.RoleId, Is.EqualTo("1"));
            Assert.That(result.Data.RoleName, Is.EqualTo("Admin"));
            Assert.That(result.Data.UserCount, Is.EqualTo(2));
        }

        [Test]
        public async Task GetRoleByIdAsync_WhenRoleDoesNotExist_ReturnsFailure()
        {
            // Arrange
            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("999"))
                .ReturnsAsync((IdentityRole?)null);

            // Act
            var result = await _roleService.GetRoleByIdAsync("999");

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors!.First(), Is.EqualTo("Role not found"));
        }

        [Test]
        public async Task GetRoleByIdAsync_WithNullId_ReturnsFailure()
        {
            // Arrange
            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((IdentityRole?)null);

            // Act
            var result = await _roleService.GetRoleByIdAsync(null!);

            // Assert
            Assert.That(result.Succeeded, Is.False);
        }

        #endregion

        #region CreateRoleAsync

        [Test]
        public async Task CreateRoleAsync_WhenRoleDoesNotExist_CreatesRole()
        {
            // Arrange
            var roleName = "Manager";

            _roleManagerMock
                .Setup(rm => rm.FindByNameAsync(roleName))
                .ReturnsAsync((IdentityRole?)null);

            _roleManagerMock
                .Setup(rm => rm.CreateAsync(It.IsAny<IdentityRole>()))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _roleService.CreateRoleAsync(roleName);

            // Assert
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.RoleName, Is.EqualTo("Manager"));
            Assert.That(result.Message, Is.EqualTo("Role created successfully"));

            // Verificar que CreateAsync fue llamado exactamente una vez
            _roleManagerMock.Verify(
                rm => rm.CreateAsync(It.Is<IdentityRole>(r => r.Name == "Manager")),
                Times.Once);
        }

        [Test]
        public async Task CreateRoleAsync_WhenRoleAlreadyExists_ReturnsFailure()
        {
            // Arrange
            var roleName = "Admin";

            _roleManagerMock
                .Setup(rm => rm.FindByNameAsync(roleName))
                .ReturnsAsync(new IdentityRole(roleName));

            // Act
            var result = await _roleService.CreateRoleAsync(roleName);

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors!.First(), Is.EqualTo("A role with this name already exists"));

            // Verificar que CreateAsync nunca fue llamado
            _roleManagerMock.Verify(
                rm => rm.CreateAsync(It.IsAny<IdentityRole>()),
                Times.Never);
        }

        [Test]
        public async Task CreateRoleAsync_WhenCreateFails_ReturnsFailure()
        {
            // Arrange
            var roleName = "Manager";

            _roleManagerMock
                .Setup(rm => rm.FindByNameAsync(roleName))
                .ReturnsAsync((IdentityRole?)null);

            _roleManagerMock
                .Setup(rm => rm.CreateAsync(It.IsAny<IdentityRole>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Code = "Error", Description = "Database error" }));

            // Act
            var result = await _roleService.CreateRoleAsync(roleName);

            // Assert
            Assert.That(result.Succeeded, Is.False);
        }

        #endregion

        #region UpdateRoleAsync

        [Test]
        public async Task UpdateRoleAsync_WhenRoleNotFound_ReturnsFailure()
        {
            // Arrange
            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync((IdentityRole?)null);

            // Act
            var result = await _roleService.UpdateRoleAsync("1", "NewName");

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors!.First(), Is.EqualTo("Role not found"));
        }

        [Test]
        public async Task UpdateRoleAsync_WhenNewNameTakenByAnotherRole_ReturnsFailure()
        {
            // Arrange
            var existingRole = new IdentityRole { Id = "1", Name = "Admin" };
            var otherRoleWithSameName = new IdentityRole { Id = "2", Name = "Manager" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(existingRole);

            _roleManagerMock
                .Setup(rm => rm.FindByNameAsync("Manager"))
                .ReturnsAsync(otherRoleWithSameName);

            // Act
            var result = await _roleService.UpdateRoleAsync("1", "Manager");

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors!.First(), Is.EqualTo("A role with this name already exists"));
        }

        [Test]
        public async Task UpdateRoleAsync_WhenSameNameAsSelf_UpdatesSuccessfully()
        {
            // Arrange - El rol se actualiza con su propio nombre (caso edge)
            var role = new IdentityRole { Id = "1", Name = "Admin", NormalizedName = "ADMIN" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            // FindByNameAsync devuelve el mismo rol (mismo ID)
            _roleManagerMock
                .Setup(rm => rm.FindByNameAsync("Admin"))
                .ReturnsAsync(role);

            _roleManagerMock
                .Setup(rm => rm.UpdateAsync(It.IsAny<IdentityRole>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<IdentityUser>());

            // Act
            var result = await _roleService.UpdateRoleAsync("1", "Admin");

            // Assert
            Assert.That(result.Succeeded, Is.True);
        }

        [Test]
        public async Task UpdateRoleAsync_WhenValid_UpdatesAndReturnsRole()
        {
            // Arrange
            var role = new IdentityRole { Id = "1", Name = "Admin", NormalizedName = "ADMIN" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            _roleManagerMock
                .Setup(rm => rm.FindByNameAsync("Manager"))
                .ReturnsAsync((IdentityRole?)null);

            _roleManagerMock
                .Setup(rm => rm.UpdateAsync(It.IsAny<IdentityRole>()))
                .ReturnsAsync(IdentityResult.Success);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Manager"))
                .ReturnsAsync(new List<IdentityUser>
                {
                    new IdentityUser { Id = "U1" }
                });

            // Act
            var result = await _roleService.UpdateRoleAsync("1", "Manager");

            // Assert
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Data, Is.Not.Null);
            Assert.That(result.Data!.RoleId, Is.EqualTo("1"));
            Assert.That(result.Data.RoleName, Is.EqualTo("Manager"));
            Assert.That(result.Data.UserCount, Is.EqualTo(1));
            Assert.That(result.Message, Is.EqualTo("Role updated successfully"));

            _roleManagerMock.Verify(rm => rm.UpdateAsync(It.IsAny<IdentityRole>()), Times.Once);
        }

        #endregion

        #region DeleteRoleAsync

        [Test]
        public async Task DeleteRoleAsync_WhenRoleNotFound_ReturnsFailure()
        {
            // Arrange
            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync((IdentityRole?)null);

            // Act
            var result = await _roleService.DeleteRoleAsync("1");

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors!.First(), Is.EqualTo("Role not found"));
        }

        [Test]
        public async Task DeleteRoleAsync_WhenRoleHasUsers_ReturnsFailure()
        {
            // Arrange
            var role = new IdentityRole { Id = "1", Name = "Admin" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<IdentityUser>
                {
                    new IdentityUser { Id = "U1" }
                });

            // Act
            var result = await _roleService.DeleteRoleAsync("1");

            // Assert
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Errors, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Errors!.First(), Does.Contain("Cannot delete role 'Admin'"));

            // Verificar que DeleteAsync nunca fue llamado
            _roleManagerMock.Verify(rm => rm.DeleteAsync(It.IsAny<IdentityRole>()), Times.Never);
        }

        [Test]
        public async Task DeleteRoleAsync_WhenRoleHasNoUsers_DeletesSuccessfully()
        {
            // Arrange
            var role = new IdentityRole { Id = "1", Name = "Admin" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<IdentityUser>());

            _roleManagerMock
                .Setup(rm => rm.DeleteAsync(role))
                .ReturnsAsync(IdentityResult.Success);

            // Act
            var result = await _roleService.DeleteRoleAsync("1");

            // Assert
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Message, Is.EqualTo("Role deleted successfully"));

            _roleManagerMock.Verify(rm => rm.DeleteAsync(role), Times.Once);
        }

        [Test]
        public async Task DeleteRoleAsync_WhenDeleteFails_ReturnsFailure()
        {
            // Arrange
            var role = new IdentityRole { Id = "1", Name = "Admin" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<IdentityUser>());

            _roleManagerMock
                .Setup(rm => rm.DeleteAsync(role))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError { Code = "Error", Description = "Database error" }));

            // Act
            var result = await _roleService.DeleteRoleAsync("1");

            // Assert
            Assert.That(result.Succeeded, Is.False);
        }

        #endregion

        #region GetUsersInRoleAsync

        [Test]
        public async Task GetUsersInRoleAsync_WhenRoleNotFound_ReturnsEmptyPaginatedResult()
        {
            // Arrange
            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync((IdentityRole?)null);

            var pagination = new PaginationQuery { Page = 1, PageSize = 10 };

            // Act
            var result = await _roleService.GetUsersInRoleAsync("1", pagination);

            // Assert
            Assert.That(result.Data, Is.Empty);
            Assert.That(result.Pagination.TotalCount, Is.EqualTo(0));
            Assert.That(result.Pagination.TotalPages, Is.EqualTo(0));
        }

        [Test]
        public async Task GetUsersInRoleAsync_WhenRoleExists_ReturnsPaginatedUsers()
        {
            // Arrange
            var role = new IdentityRole { Id = "1", Name = "Admin" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            var users = new List<IdentityUser>
            {
                new IdentityUser { Id = "U1", UserName = "user1", Email = "u1@test.com" },
                new IdentityUser { Id = "U2", UserName = "user2", Email = "u2@test.com" },
                new IdentityUser { Id = "U3", UserName = "user3", Email = "u3@test.com" },
            };

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(users);

            _userManagerMock
                .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
                .ReturnsAsync(new List<string> { "Admin" });

            var pagination = new PaginationQuery { Page = 1, PageSize = 2 };

            // Act
            var result = await _roleService.GetUsersInRoleAsync("1", pagination);

            // Assert
            Assert.That(result.Data.Count(), Is.EqualTo(2));
            Assert.That(result.Pagination.TotalCount, Is.EqualTo(3));
            Assert.That(result.Pagination.TotalPages, Is.EqualTo(2));

            var firstPageUsers = result.Data.ToList();
            Assert.That(firstPageUsers[0].UserName, Is.EqualTo("user1"));
            Assert.That(firstPageUsers[1].UserName, Is.EqualTo("user2"));
        }

        [Test]
        public async Task GetUsersInRoleAsync_SecondPage_ReturnsCorrectUsers()
        {
            // Arrange
            var role = new IdentityRole { Id = "1", Name = "Admin" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            var users = new List<IdentityUser>
            {
                new IdentityUser { Id = "U1", UserName = "user1", Email = "u1@test.com" },
                new IdentityUser { Id = "U2", UserName = "user2", Email = "u2@test.com" },
                new IdentityUser { Id = "U3", UserName = "user3", Email = "u3@test.com" },
            };

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(users);

            _userManagerMock
                .Setup(um => um.GetRolesAsync(It.IsAny<IdentityUser>()))
                .ReturnsAsync(new List<string> { "Admin" });

            var pagination = new PaginationQuery { Page = 2, PageSize = 2 };

            // Act
            var result = await _roleService.GetUsersInRoleAsync("1", pagination);

            // Assert
            Assert.That(result.Data.Count(), Is.EqualTo(1)); // Solo 1 usuario en página 2
            Assert.That(result.Pagination.TotalCount, Is.EqualTo(3));
            Assert.That(result.Pagination.TotalPages, Is.EqualTo(2));

            var secondPageUsers = result.Data.ToList();
            Assert.That(secondPageUsers[0].UserName, Is.EqualTo("user3"));
        }

        [Test]
        public async Task GetUsersInRoleAsync_WhenNoUsersInRole_ReturnsEmptyList()
        {
            // Arrange
            var role = new IdentityRole { Id = "1", Name = "Admin" };

            _roleManagerMock
                .Setup(rm => rm.FindByIdAsync("1"))
                .ReturnsAsync(role);

            _userManagerMock
                .Setup(um => um.GetUsersInRoleAsync("Admin"))
                .ReturnsAsync(new List<IdentityUser>());

            var pagination = new PaginationQuery { Page = 1, PageSize = 10 };

            // Act
            var result = await _roleService.GetUsersInRoleAsync("1", pagination);

            // Assert
            Assert.That(result.Data, Is.Empty);
            Assert.That(result.Pagination.TotalCount, Is.EqualTo(0));
        }

        #endregion
    }
}