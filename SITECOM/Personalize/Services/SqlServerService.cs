using Microsoft.Data.SqlClient;
using Personalize.Models;

namespace Personalize.Services;

public class SqlServerService
{
    private readonly string _connectionString;

    public SqlServerService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Purchase>> GetPurchasesAsync(DateTime? fromDate = null)
    {
        var purchases = new Dictionary<int, Purchase>();

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Query para buscar compras e produtos
        var query = @"
            SELECT 
                c.PKId,
                c.PKIdUsuario,
                c.status,
                c.idDados,
                c.data,
                c.dataMdSt,
                pc.idUsuario,
                pc.idProduto,
                pc.quantidade,
                pc.PKId as ProductPurchasePKId,
                pc.PKIdCompra,
                pc.preco,
                pc.nome,
                pc.sys_creation_date,
                pc.sys_update_date
            FROM tbCompra c
            INNER JOIN tbProdutosCompra pc ON pc.PKIdCompra = c.PKId
            WHERE c.status = 'V'";

        if (fromDate.HasValue)
        {
            // Nas execuções incrementais, usar dataMdSt (data de modificação) ao invés de data
            // Se dataMdSt for NULL, usar data como fallback
            query += " AND COALESCE(c.dataMdSt, c.data) >= @fromDate";
        }

        query += " ORDER BY c.PKId, pc.idProduto";

        using var command = new SqlCommand(query, connection);
        if (fromDate.HasValue)
        {
            command.Parameters.AddWithValue("@fromDate", fromDate.Value);
        }

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var purchaseId = reader.GetInt32(0);

            if (!purchases.ContainsKey(purchaseId))
            {
                purchases[purchaseId] = new Purchase
                {
                    PKId = purchaseId,
                    PKIdUsuario = reader.GetInt32(1),
                    Status = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    IdDados = reader.GetInt32(3),
                    Data = reader.GetDateTime(4),
                    DataMdSt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                };
            }

            var productPurchase = new ProductPurchase
            {
                IdUsuario = reader.GetInt32(6),
                IdProduto = reader.GetInt32(7),
                Quantidade = reader.GetInt32(8),
                PKId = reader.GetInt32(9),
                PKIdCompra = reader.GetInt32(10),
                Preco = reader.GetDecimal(11),
                Nome = reader.IsDBNull(12) ? string.Empty : reader.GetString(12),
                SysCreationDate = reader.GetDateTime(13),
                SysUpdateDate = reader.IsDBNull(14) ? null : reader.GetDateTime(14)
            };

            purchases[purchaseId].Products.Add(productPurchase);
        }

        return purchases.Values.ToList();
    }
}

