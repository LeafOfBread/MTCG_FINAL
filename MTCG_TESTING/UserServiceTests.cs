using Moq;
using Npgsql;
using SWE.Models;
using System.Threading.Tasks;
using Xunit;

public class UserServiceTests
{
    private readonly Mock<NpgsqlConnection> _mockConnection;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _mockConnection = new Mock<NpgsqlConnection>("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432");
        _userService = new UserService(_mockConnection.Object);
    }

    [Fact]
    public async Task AuthenticateUserAsync_ShouldReturnSuccess_WhenUserCredentialsAreValid()
    {
        // Arrange
        var username = "testUser";
        var password = "testPassword";
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

        var mockUser = new User
        {
            id = 1,
            username = username,
            password = hashedPassword,
            token = "testToken",
            coins = 100,
            wins = 5,
            losses = 2
        };

        _mockConnection.Setup(conn => conn.OpenAsync(It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userServiceMock = new Mock<UserService>(_mockConnection.Object);
        userServiceMock.Setup(service => service.GetUserByUsernameAsync(username))
            .ReturnsAsync(mockUser);

        // Act
        var result = await _userService.AuthenticateUserAsync(username, password);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Token);
    }

    [Fact]
    public async Task AuthenticateUserAsync_ShouldReturnFailure_WhenUserCredentialsAreInvalid()
    {
        // Arrange
        var username = "nonexistentUser";
        var password = "wrongPassword";

        _mockConnection.Setup(conn => conn.OpenAsync(It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userServiceMock = new Mock<UserService>(_mockConnection.Object);
        userServiceMock.Setup(service => service.GetUserByUsernameAsync(username))
            .ReturnsAsync((User)null);

        // Act
        var result = await _userService.AuthenticateUserAsync(username, password);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Null(result.Token);
    }

    [Fact]
    public async Task RegisterUserAsync_ShouldReturnSuccess_WhenUserIsNew()
    {
        // Arrange
        var newUser = new User
        {
            username = "newUser",
            password = "newPassword"
        };

        _mockConnection.Setup(conn => conn.OpenAsync(It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userServiceMock = new Mock<UserService>(_mockConnection.Object);
        userServiceMock.Setup(service => service.GetUserByUsernameAsync(newUser.username))
            .ReturnsAsync((User)null);

        // Act
        var result = await _userService.RegisterUserAsync(newUser);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RegisterUserAsync_ShouldReturnFailure_WhenUserAlreadyExists()
    {
        // Arrange
        var existingUser = new User
        {
            username = "existingUser",
            password = "existingPassword"
        };

        _mockConnection.Setup(conn => conn.OpenAsync(It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userServiceMock = new Mock<UserService>(_mockConnection.Object);
        userServiceMock.Setup(service => service.GetUserByUsernameAsync(existingUser.username))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _userService.RegisterUserAsync(existingUser);

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var username = "existingUser";
        var mockUser = new User
        {
            id = 1,
            username = username,
            password = "hashedPassword",
            token = "token",
            coins = 100,
            wins = 5,
            losses = 2
        };

        _mockConnection.Setup(conn => conn.OpenAsync(It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userServiceMock = new Mock<UserService>(_mockConnection.Object);
        userServiceMock.Setup(service => service.GetUserByUsernameAsync(username))
            .ReturnsAsync(mockUser);

        // Act
        var result = await _userService.GetUserByUsernameAsync(username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(username, result.username);
    }

    [Fact]
    public async Task GetUserByUsernameAsync_ShouldReturnNull_WhenUserDoesNotExist()
    {
        // Arrange
        var username = "nonexistentUser";

        _mockConnection.Setup(conn => conn.OpenAsync(It.IsAny<System.Threading.CancellationToken>()))
            .Returns(Task.CompletedTask);

        var userServiceMock = new Mock<UserService>(_mockConnection.Object);
        userServiceMock.Setup(service => service.GetUserByUsernameAsync(username))
            .ReturnsAsync((User)null);

        // Act
        var result = await _userService.GetUserByUsernameAsync(username);

        // Assert
        Assert.Null(result);
    }
}
