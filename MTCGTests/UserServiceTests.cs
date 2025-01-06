using NUnit.Framework;
using Npgsql;
using SWE.Models;
using System.Threading.Tasks;
using Moq;

[TestFixture]
public class UserServiceTests
{
    private UserService _userService;
    private NpgsqlConnection _connection;

    private const string ConnectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg_test;Port=5432";

    [SetUp]
    public void SetUp()
    {
        _connection = new NpgsqlConnection(ConnectionString);
        _userService = new UserService(_connection);

        _connection.Open();

        CleanUpDatabase();
    }

    [TearDown]
    public void TearDown()
    {
        _connection.Close();
    }

    private void CleanUpDatabase()
    {
        using (var cmd = new NpgsqlCommand("DELETE FROM users;", _connection))
        {
            cmd.ExecuteNonQuery();
        }
    }

    [Test]
    public async Task GetUserByUsernameAsync_ShouldReturnUser_WhenUserExists()
    {
        var username = "testuser";
        var expectedUser = new User
        {
            id = 1,
            username = "testuser",
            password = "hashedpassword",
            token = "user-token",
            coins = 100,
            wins = 5,
            losses = 2
        };

        var insertCmd = new NpgsqlCommand("INSERT INTO users (username, password, token, coins, wins, losses) VALUES (@username, @password, @token, @coins, @wins, @losses)", _connection);
        insertCmd.Parameters.AddWithValue("username", expectedUser.username);
        insertCmd.Parameters.AddWithValue("password", expectedUser.password);
        insertCmd.Parameters.AddWithValue("token", expectedUser.token);
        insertCmd.Parameters.AddWithValue("coins", expectedUser.coins);
        insertCmd.Parameters.AddWithValue("wins", expectedUser.wins);
        insertCmd.Parameters.AddWithValue("losses", expectedUser.losses);
        insertCmd.ExecuteNonQuery();

        var result = await _userService.GetUserByUsernameAsync(username);

        Assert.IsNotNull(result);
        Assert.AreEqual(expectedUser.username, result.username);
    }
    [Test]
    public async Task AuthenticateUserAsync_ShouldAuthenticate_WhenCorrectCredentials()
    {
        // Arrange
        var username = "testuser";
        var password = "correctpassword";
        var expectedToken = "testuser-mtcgToken";

        var user = new User
        {
            id = 1,
            username = "testuser",
            password = BCrypt.Net.BCrypt.HashPassword("correctpassword"),
            token = "user-token",
            coins = 100,
            wins = 5,
            losses = 2
        };

        var insertCmd = new NpgsqlCommand("INSERT INTO users (username, password, token, coins, wins, losses) VALUES (@username, @password, @token, @coins, @wins, @losses)", _connection);
        insertCmd.Parameters.AddWithValue("username", user.username);
        insertCmd.Parameters.AddWithValue("password", user.password);
        insertCmd.Parameters.AddWithValue("token", user.token);
        insertCmd.Parameters.AddWithValue("coins", user.coins);
        insertCmd.Parameters.AddWithValue("wins", user.wins);
        insertCmd.Parameters.AddWithValue("losses", user.losses);
        insertCmd.ExecuteNonQuery();

        var result = await _userService.AuthenticateUserAsync(username, password);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(expectedToken, result.Token);
    }

    [Test]
    public async Task GetUserByUsernameAsync_ReturnsNull_WhenUsernameDoesNotExist()
    {
        // Arrange
        var mockConnection = new Mock<NpgsqlConnection>();
        var userService = new UserService(mockConnection.Object);

        var username = "nonExistentUser";

        mockConnection.Setup(conn => conn.State).Returns(System.Data.ConnectionState.Open);
        mockConnection.Setup(conn => conn.OpenAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await userService.GetUserByUsernameAsync(username);

        // Assert
        Assert.Null(result);
    }

    [TestFixture]
    public class TokenServiceTests
    {
        private TokenService _tokenService;

        [SetUp]
        public void Setup()
        {
            _tokenService = new TokenService();
        }

        [Test]
        public void GenerateToken_ReturnsToken_WhenValidUser()
        {
            // Arrange
            var user = new User { username = "testuser" };

            // Act
            var token = _tokenService.GenerateToken(user);

            // Assert
            Assert.IsNotNull(token);
            Assert.IsNotEmpty(token);
        }

        [Test]
        public void ValidateToken_ReturnsTrue_WhenTokensAreEqual()
        {
            // Arrange
            var token1 = "validToken123";
            var token2 = "validToken123";

            // Act
            var isValid = _tokenService.ValidateToken(token1, token2);

            // Assert
            Assert.IsTrue(isValid);
        }

        [Test]
        public void ValidateToken_ReturnsFalse_WhenTokensAreNotEqual()
        {
            // Arrange
            var token1 = "validToken123";
            var token2 = "invalidToken456";

            // Act
            var isValid = _tokenService.ValidateToken(token1, token2);

            // Assert
            Assert.IsFalse(isValid);
        }
    }
}
