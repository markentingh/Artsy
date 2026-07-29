using Dapper;
using System.Data;
using System.Text.Json;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintVariantRepository : IPrintifyBlueprintVariantRepository
    {
        readonly IDbConnection _dbConnection;

        public PrintifyBlueprintVariantRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintAndProviderAsync(int blueprintId, int printProviderId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintVariants"" WHERE ""BlueprintId"" = @blueprintId AND ""PrintProviderId"" = @printProviderId ORDER BY ""Color""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintVariant>(query, new { blueprintId, printProviderId });
        }

        public async Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintIdAsync(int blueprintId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintVariants"" WHERE ""BlueprintId"" = @blueprintId ORDER BY ""Color""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintVariant>(query, new { blueprintId });
        }

        public async Task<IEnumerable<PrintifyBlueprintVariant>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds)
        {
            var ids = blueprintIds.ToList();
            if (ids.Count == 0) return Enumerable.Empty<PrintifyBlueprintVariant>();
            const string query = @"SELECT * FROM public.""PrintifyBlueprintVariants"" WHERE ""BlueprintId"" = ANY(@blueprintIds) ORDER BY ""BlueprintId"", ""Color""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintVariant>(query, new { blueprintIds = ids.ToArray() });
        }

        public async Task UpsertBatchAsync(IEnumerable<PrintifyBlueprintVariant> variants)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintVariants"" (""VariantId"", ""BlueprintId"", ""PrintProviderId"", ""Color"", ""Options"", ""Size"", ""DecorationMethods"", ""DateUpdated"")
                VALUES (@VariantId, @BlueprintId, @PrintProviderId, @Color, @Options, @Size, @DecorationMethods, CURRENT_TIMESTAMP)
                ON CONFLICT (""VariantId"")
                DO UPDATE SET
                    ""BlueprintId"" = @BlueprintId,
                    ""PrintProviderId"" = @PrintProviderId,
                    ""Color"" = @Color,
                    ""Options"" = @Options,
                    ""Size"" = @Size,
                    ""DecorationMethods"" = @DecorationMethods,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, variants);
        }

        public async Task DeleteByBlueprintAndProviderAsync(int blueprintId, int printProviderId)
        {
            const string query = @"DELETE FROM public.""PrintifyBlueprintVariants"" WHERE ""BlueprintId"" = @blueprintId AND ""PrintProviderId"" = @printProviderId";
            await _dbConnection.ExecuteAsync(query, new { blueprintId, printProviderId });
        }

        public async Task<int> ConvertVariantsAsync()
        {
            const string selectQuery = @"SELECT ""VariantId"", ""Options"" FROM public.""PrintifyBlueprintVariants""";
            var rows = (await _dbConnection.QueryAsync<(int VariantId, string Options)>(selectQuery)).ToList();

            int updated = 0;
            foreach (var row in rows)
            {
                string color = "";
                string size = "";
                try
                {
                    if (!string.IsNullOrWhiteSpace(row.Options))
                    {
                        using var doc = JsonDocument.Parse(row.Options);
                        if (doc.RootElement.TryGetProperty("color", out var colorEl))
                            color = colorEl.GetString() ?? "";
                        if (doc.RootElement.TryGetProperty("size", out var sizeEl))
                            size = sizeEl.GetString() ?? "";
                    }
                }
                catch { }

                const string updateQuery = @"
                    UPDATE public.""PrintifyBlueprintVariants"" 
                    SET ""Color"" = @color, ""Size"" = @size 
                    WHERE ""VariantId"" = @variantId";
                updated += await _dbConnection.ExecuteAsync(updateQuery, new { color, size, variantId = row.VariantId });
            }

            return updated;
        }
    }
}
