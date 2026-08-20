using Microsoft.Data.SqlClient;
using System.Data;

namespace HealthConnect.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddRange(parameters);
            return await command.ExecuteNonQueryAsync();
        }

        public async Task<T?> ExecuteScalarAsync<T>(string sql, params SqlParameter[] parameters)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddRange(parameters);
            var result = await command.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value) return default;
            return (T)Convert.ChangeType(result, typeof(T));
        }

        public async Task<List<Dictionary<string, object>>> QueryAsync(string sql, params SqlParameter[] parameters)
        {
            var results = new List<Dictionary<string, object>>();
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddRange(parameters);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                results.Add(row);
            }
            return results;
        }

        public async Task<Dictionary<string, object>?> QuerySingleAsync(string sql, params SqlParameter[] parameters)
        {
            var results = await QueryAsync(sql, parameters);
            return results.FirstOrDefault();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                return true;
            }
            catch { return false; }
        }
    }

    public class DataAccessException : Exception
    {
        public DataAccessException(string message, Exception inner)
            : base(message, inner) { }
    }
}