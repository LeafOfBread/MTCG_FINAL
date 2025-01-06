using Npgsql;
using SWE.Models;
using System.Data.SqlClient;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

public class UserService
{
    private readonly NpgsqlConnection _connection;

    public UserService(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<AuthenticationResult> AuthenticateTokenAsync(string token)
    {
        try
        {
            // Validate token by checking if it matches the stored token
            var user = await GetUserByTokenAsync(token);

            if (user == null)
            {
                return new AuthenticationResult { IsAuthenticated = false };
            }

            return new AuthenticationResult
            {
                IsAuthenticated = true,
                User = user,
                Token = token // Optionally return the token if needed
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in AuthenticateTokenAsync: {ex.Message}");
            return new AuthenticationResult { IsAuthenticated = false };
        }
    }

    public async Task<User> GetUserByTokenAsync(string token)
    {
        const string query = "SELECT * FROM users WHERE token = @token";
        using (var connection = new NpgsqlConnection("Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432"))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@token", token);
                Console.WriteLine("Executing query to fetch user by token...");
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (reader.Read())
                    {
                        Console.WriteLine("User found, returning user.");
                        return new User
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            coins = reader.GetInt32(reader.GetOrdinal("coins")),
                            username = reader.GetString(reader.GetOrdinal("username")),
                            password = reader.GetString(reader.GetOrdinal("password")),
                            token = reader.GetString(reader.GetOrdinal("token")),
                            wins = reader.GetInt32(reader.GetOrdinal("wins")),
                            losses = reader.GetInt32(reader.GetOrdinal("losses")),
                        };
                    }
                    Console.WriteLine("User not found.");
                    return null;
                }
            }
        }
    }


    public async Task<User> GetUserByUsernameAsync(string username)
    {
        const string query = "SELECT * FROM users WHERE username = @username";

        using (var command = new NpgsqlCommand(query, _connection))
        {
            command.Parameters.AddWithValue("@username", username);

            // Ensure the connection is open
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return new User
                    {
                        id = reader.GetInt32(reader.GetOrdinal("id")),
                        username = reader.GetString(reader.GetOrdinal("username")),
                        password = reader.GetString(reader.GetOrdinal("password")),
                        token = reader.GetString(reader.GetOrdinal("token")),
                        coins = reader.GetInt32(reader.GetOrdinal("coins")),
                        wins = reader.GetInt32(reader.GetOrdinal("wins")),
                        losses = reader.GetInt32(reader.GetOrdinal("losses")),
                    };
                }
            }
        }

        return null;
    }

    public async Task SaveUserAsync(User user)
    {
        const string query = "INSERT INTO users (username, password, token) VALUES (@username, @password, @token)";

        using (var command = new NpgsqlCommand(query, _connection))
        {
            command.Parameters.AddWithValue("@username", user.username);
            command.Parameters.AddWithValue("@password", user.password);
            command.Parameters.AddWithValue("@token", user.token);

            // Ensure the connection is open
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task DeductCoinsFromUserAsync(User user, int amount)
    {
        const string query = "UPDATE users SET coins = coins - @amount WHERE id = @id";
        using (var command = new NpgsqlCommand(query, _connection))
        {
            command.Parameters.AddWithValue("@amount", amount);
            command.Parameters.AddWithValue("@id", user.id);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task UpdateUserTokenAsync(int userId, string token)
    {
        const string query = "UPDATE users SET token = @token WHERE id = @id";

        using (var command = new NpgsqlCommand(query, _connection))
        {
            command.Parameters.AddWithValue("@token", token);
            command.Parameters.AddWithValue("@id", userId);

            // Ensure the connection is open
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await command.ExecuteNonQueryAsync();
        }
    }
    public async Task<AuthenticationResult> AuthenticateUserAsync(string username, string password)
    {
        // Fetch the user by username
        var user = await GetUserByUsernameAsync(username);
        if (user == null || !VerifyPasswordHash(password, user.password))
        {
            Console.WriteLine("400: Login Failed!\n");
            return new AuthenticationResult { IsSuccess = false, Token = null };
        }

        // Generate a simple token (for example, just the username, or a random token)
        string token = GenerateToken(user);

        Console.WriteLine("200: User Logged In \n");

        return new AuthenticationResult { IsSuccess = true, Token = token };
    }

    public async Task<RegistrationResult> RegisterUserAsync(User user)
    {
        // Check if the user already exists by username
        var existingUser = await GetUserByUsernameAsync(user.username);
        if (existingUser != null)
        {
            Console.WriteLine("400: User already exists!\n");
            return new RegistrationResult { IsSuccess = false };
        }

        // Hash the password before saving the user
        user.password = HashPassword(user.password);
        user.token = GenerateToken(user);

        // Save the user to the repository (e.g., a database)
        await SaveUserAsync(user);
        Console.WriteLine("201: User Created\n");

        return new RegistrationResult { IsSuccess = true };
    }
    public bool VerifyPasswordHash(string enteredPassword, string storedHash)
    {
        return BCrypt.Net.BCrypt.Verify(enteredPassword, storedHash);
    }

    public string HashPassword(string password)
    {
        // Hash the password using BCrypt with a random salt
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private string GenerateToken(User user)
    {
        return user.username + "-mtcgToken";
    }

    public class RegistrationResult
    {
        public bool IsSuccess { get; set; }
    }
}
