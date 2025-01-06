using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SWE.Models
{
    public class Database
    {
        public Database()
        {
            string _connectionString = $"Host={host};Username={username};Password={password};Database={database};Port=5432";
            connection = new NpgsqlConnection(_connectionString);
        }

        private string host = "localhost";
        private string username = "postgres";
        private string password = "fhtw";
        private string database = "mtcg";

        public string _connectionString;

        private NpgsqlConnection connection;

        public async Task<List<User>> GetAllUsersAsync()
        {
            const string query = "SELECT * FROM users";
            var users = new List<User>();
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(query, connection))
                {
                    // Ensure connection is open
                    if (connection.State == ConnectionState.Closed)
                        await connection.OpenAsync();

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(new User
                            {
                                id = reader.GetInt32(reader.GetOrdinal("id")),
                                username = reader.GetString(reader.GetOrdinal("username")),
                                password = reader.GetString(reader.GetOrdinal("password")),
                                token = reader.IsDBNull(reader.GetOrdinal("token")) ? null : reader.GetString(reader.GetOrdinal("token"))
                            });
                        }
                    }
                }
            }

            return users;
        }

        public async Task<User> GetUserByIdAsync(int userId)
        {
            const string query = "SELECT * FROM users WHERE id = @id";
            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@id", userId);
                if (connection.State == ConnectionState.Closed)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new User
                        {
                            id = reader.GetInt32(reader.GetOrdinal("id")),
                            username = reader.GetString(reader.GetOrdinal("username")),
                            password = reader.GetString(reader.GetOrdinal("password")),
                            token = reader.IsDBNull(reader.GetOrdinal("token")) ? null : reader.GetString(reader.GetOrdinal("token"))
                        };
                    }
                }
            }

            return null;
        }

        public async Task AddUserAsync(User user)
        {
            const string query = "INSERT INTO users (username, password, token) VALUES (@username, @password, @token)";

            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@username", user.username);
                command.Parameters.AddWithValue("@password", user.password);
                command.Parameters.AddWithValue("@token", user.token ?? (object)DBNull.Value);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task UpdateUserAsync(User user)
        {
            const string query = "UPDATE users SET username = @username, password = @password, token = @token WHERE id = @id";

            using (var connection = new NpgsqlConnection(_connectionString))
            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@id", user.id);
                command.Parameters.AddWithValue("@username", user.username);
                command.Parameters.AddWithValue("@password", user.password);
                command.Parameters.AddWithValue("@token", user.token ?? (object)DBNull.Value);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteUserAsync(int userId)
        {
            const string query = "DELETE FROM users WHERE id = @id";

            using (var connection = new NpgsqlConnection(_connectionString))
            using (var command = new NpgsqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@id", userId);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<Card>> GetAllCardsAsync()
        {
            const string query = "SELECT * FROM cards";
            var cards = new List<Card>();

            using (var connection = new NpgsqlConnection(_connectionString))
            using (var command = new NpgsqlCommand(query, connection))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        cards.Add(new Card(connection)
                        {
                            id = reader.GetGuid(reader.GetOrdinal("id")), // Get Id as a string
                            name = reader.GetString(reader.GetOrdinal("name")),
                            damage = reader.GetDouble(reader.GetOrdinal("damage")), // Get Damage as a double
                            
                        });
                    }
                }
            }

            return cards;
        }

    }

    // Example of entities used
    

    
}
