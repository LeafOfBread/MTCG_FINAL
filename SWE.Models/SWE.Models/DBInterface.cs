using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWE.Models
{
    public interface IDatabaseConnection
    {
        Task OpenAsync();
        Task<int> ExecuteScalarAsync(string query, NpgsqlParameter[] parameters);
        Task ExecuteNonQueryAsync(string query, NpgsqlParameter[] parameters);
    }

    public class NpgsqlConnectionWrapper : IDatabaseConnection
    {
        private readonly NpgsqlConnection _connection;

        public NpgsqlConnectionWrapper(NpgsqlConnection connection)
        {
            _connection = connection;
        }

        public Task OpenAsync() => _connection.OpenAsync();
        public Task<int> ExecuteScalarAsync(string query, params NpgsqlParameter[] parameters)
        {
            using (var command = new NpgsqlCommand(query, _connection))
            {
                command.Parameters.AddRange(parameters);
                return Task.FromResult((int)command.ExecuteScalar());
            }
        }

        public Task ExecuteNonQueryAsync(string query, params NpgsqlParameter[] parameters)
        {
            using (var command = new NpgsqlCommand(query, _connection))
            {
                command.Parameters.AddRange(parameters);
                return command.ExecuteNonQueryAsync();
            }
        }
    }

}
