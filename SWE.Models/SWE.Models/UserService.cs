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
    public string connectionString = "Host=localhost;Username=postgres;Password=fhtw;Database=mtcg;Port=5432";

            
    public async Task<User> GetUserByUsernameAsync(string username)
    {
        const string query = "SELECT * FROM users WHERE username = @username";

        using (var command = new NpgsqlCommand(query, _connection))
        {
            command.Parameters.AddWithValue("@username", username);

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

    public async Task<int> GetUserIdByTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("Token must not be null or empty.", nameof(token));

        const string query = "SELECT * FROM users WHERE token = @token";

        int returningId;

        using (var command = new NpgsqlCommand(query, _connection))
        {
            command.Parameters.AddWithValue("@token", token);

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    returningId = reader.GetInt32(reader.GetOrdinal("id"));
                    return returningId;
                }
            }
        }
        return -1; //return value fuer error handling
    }

    public async Task SaveUserAsync(User user)
    {
        const string query = "INSERT INTO users (username, password, token) VALUES (@username, @password, @token)";

        using (var command = new NpgsqlCommand(query, _connection))
        {
            command.Parameters.AddWithValue("@username", user.username);
            command.Parameters.AddWithValue("@password", user.password);
            command.Parameters.AddWithValue("@token", user.token);

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task<AuthenticationResult> AuthenticateUserAsync(string username, string password)
    {
        var user = await GetUserByUsernameAsync(username);
        if (user == null || !VerifyPasswordHash(password, user.password))
        {
            Console.WriteLine("400: Login Failed!\n");
            return new AuthenticationResult { IsSuccess = false, Token = null };
        }

        string token = GenerateToken(user);

        Console.WriteLine("200: User Logged In \n");

        return new AuthenticationResult { IsSuccess = true, Token = token };
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        string query = "SELECT id, username, password, elo, coins, wins, losses FROM users WHERE id = @userId LIMIT 1";

        using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@userId", userId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new User
                        {
                            id = reader.GetInt32(0),
                            username = reader.GetString(1),
                            password = reader.GetString(2),
                            elo = reader.GetInt32(3),
                            coins = reader.GetInt32(4),
                            wins = reader.GetInt32(5),
                            losses = reader.GetInt32(6)
                        };
                    }
                }
            }
        }
        return null;
    }


    public async Task<Deck> GetDeckForUser(User user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user), "User cannot be null");
        }

        var deck = new Deck();

        using (var connection = new NpgsqlConnection(connectionString))
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var query = "SELECT c.id, c.name FROM cards c " +
                        "INNER JOIN user_decks ud ON ud.card_id = c.id " +
                        "WHERE ud.user_id = @userId";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@userId", user.id);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var card = new Card(connection)
                        {
                            id = reader.GetGuid(reader.GetOrdinal("id")),
                            name = reader.GetString(reader.GetOrdinal("name"))
                        };

                        deck.Cards.Add(card);
                    }
                }
            }
        }

        return deck;
    }

    public async Task UpdateUserAsync(User user)
    {
        using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();

            var query = "UPDATE users SET wins = @wins, losses = @losses, draws = @draws, elo = @elo " +
                        "WHERE id = @userId";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@wins", user.wins);
                command.Parameters.AddWithValue("@losses", user.losses);
                command.Parameters.AddWithValue("@elo", user.elo);
                command.Parameters.AddWithValue("@userId", user.id);

                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<RegistrationResult> RegisterUserAsync(User user)
    {
        // Check ob user bereits existiert
        var existingUser = await GetUserByUsernameAsync(user.username);
        if (existingUser != null)
        {
            Console.WriteLine("400: User already exists!\n");
            return new RegistrationResult { IsSuccess = false };
        }

        // Hash das passwort
        user.password = HashPassword(user.password);
        user.token = GenerateToken(user);

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