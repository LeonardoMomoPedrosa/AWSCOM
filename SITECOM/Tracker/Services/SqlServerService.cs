using Microsoft.Data.SqlClient;
using Tracker.Models;

namespace Tracker.Services;

public class SqlServerService
{
    private readonly string _connectionString;

    public SqlServerService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<NewTrackingRecord>> GetNewTrackingRecordsAsync()
    {
        var records = new List<NewTrackingRecord>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string query = @"
            SELECT 
                tc.orderid,
                c.via,
                c.track,
                u.email,
                u.nome
            FROM tbtrackcontrol tc
            JOIN tbcompra c ON c.pkid = tc.orderid
            JOIN tbUsuarios u ON u.id = c.PKIdusuario
            WHERE tc.status = 0";

        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            records.Add(new NewTrackingRecord
            {
                OrderId = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                Via = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                Track = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Email = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                Nome = reader.IsDBNull(4) ? string.Empty : reader.GetString(4)
            });
        }

        return records;
    }

    public async Task UpdateTrackingStatusAsync(int orderId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        const string query = "UPDATE tbtrackcontrol SET status = 1 WHERE orderid = @orderId";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@orderId", orderId);

        await command.ExecuteNonQueryAsync();
    }
}

