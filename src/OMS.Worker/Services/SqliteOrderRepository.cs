using Microsoft.Data.Sqlite;
using OMS.Worker.Models;

namespace OMS.Worker.Services;

public sealed class SqliteOrderRepository : IOrderRepository
{
    private readonly string connectionString;

    public SqliteOrderRepository(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS order_status (
                order_id TEXT PRIMARY KEY,
                status INTEGER NOT NULL,
                message TEXT NULL,
                rrn TEXT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public void Save(OrderStatusView order)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO order_status (order_id, status, message, rrn, updated_at)
            VALUES ($orderId, $status, $message, $rrn, $updatedAt)
            ON CONFLICT(order_id) DO UPDATE SET
                status = excluded.status,
                message = excluded.message,
                rrn = excluded.rrn,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$orderId", order.OrderId);
        command.Parameters.AddWithValue("$status", (int)order.Status);
        command.Parameters.AddWithValue("$message", (object?)order.Message ?? DBNull.Value);
        command.Parameters.AddWithValue("$rrn", (object?)order.Rrn ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public OrderStatusView? Get(string orderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status, message, rrn
            FROM order_status
            WHERE order_id = $orderId;
            """;
        command.Parameters.AddWithValue("$orderId", orderId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new OrderStatusView(
            orderId,
            (OrderStatus)reader.GetInt32(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}