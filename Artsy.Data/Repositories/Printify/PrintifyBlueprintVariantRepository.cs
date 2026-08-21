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
                INSERT INTO public.""PrintifyBlueprintVariants"" (""VariantId"", ""BlueprintId"", ""PrintProviderId"", ""Color"", ""HexColor"", ""Options"", ""Size"", ""DecorationMethods"", ""DateUpdated"")
                VALUES (@VariantId, @BlueprintId, @PrintProviderId, @Color, @HexColor, @Options, @Size, @DecorationMethods, CURRENT_TIMESTAMP)
                ON CONFLICT (""VariantId"")
                DO UPDATE SET
                    ""BlueprintId"" = @BlueprintId,
                    ""PrintProviderId"" = @PrintProviderId,
                    ""Color"" = @Color,
                    ""HexColor"" = COALESCE(NULLIF(EXCLUDED.""HexColor"", ''), ""PrintifyBlueprintVariants"".""HexColor""),
                    ""Options"" = @Options,
                    ""Size"" = @Size,
                    ""DecorationMethods"" = @DecorationMethods,
                    ""DateUpdated"" = CURRENT_TIMESTAMP";
            await _dbConnection.ExecuteAsync(query, variants);
        }

        public async Task UpdateHexColorsAsync(int blueprintId, int printProviderId, IEnumerable<(string Color, string HexColor)> colorHexValues)
        {
            const string query = @"UPDATE public.""PrintifyBlueprintVariants"" SET ""HexColor"" = @hexColor, ""DateUpdated"" = CURRENT_TIMESTAMP WHERE ""BlueprintId"" = @blueprintId AND ""PrintProviderId"" = @printProviderId AND LOWER(""Color"") = LOWER(@color)";
            foreach (var (color, hexColor) in colorHexValues)
            {
                await _dbConnection.ExecuteAsync(query, new { blueprintId, printProviderId, color, hexColor });
            }
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

        public async Task<int> LoadVariantOptionsAsync()
        {
            var columns = new[] { "Depth", "Design", "Finish", "Flavor", "Hands", "Length", "Material", "Paper", "Quantity", "Scent", "Shape", "Surface", "Type", "Voltage", "Weight" };
            const string selectQuery = @"SELECT ""VariantId"", ""Options"" FROM public.""PrintifyBlueprintVariants""";
            var rows = (await _dbConnection.QueryAsync<(int VariantId, string Options)>(selectQuery)).ToList();

            var setClauses = string.Join(", ", columns.Select(c => $"\"{c}\" = @{c}"));
            var updateQuery = $@"UPDATE public.""PrintifyBlueprintVariants"" SET {setClauses}, ""DateUpdated"" = CURRENT_TIMESTAMP WHERE ""VariantId"" = @VariantId";

            int updated = 0;
            foreach (var row in rows)
            {
                var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(row.Options))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(row.Options);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in doc.RootElement.EnumerateObject())
                            {
                                if (columns.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                                {
                                    values[property.Name] = GetJsonValueAsString(property.Value);
                                }
                            }
                        }
                    }
                    catch { }
                }

                var parameters = new DynamicParameters();
                parameters.Add("VariantId", row.VariantId);
                foreach (var col in columns)
                {
                    parameters.Add(col, values.TryGetValue(col, out var v) ? v : null, dbType: System.Data.DbType.String);
                }
                updated += await _dbConnection.ExecuteAsync(updateQuery, parameters);
            }
            return updated;
        }

        static string? GetJsonValueAsString(JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => el.GetRawText()
            };
        }

        public async Task<IEnumerable<(int BlueprintId, int PrintProviderId)>> GetDistinctBlueprintProvidersWithEmptyColorOrSizeAsync()
        {
            const string query = @"
                SELECT DISTINCT ""BlueprintId"", ""PrintProviderId"" 
                FROM public.""PrintifyBlueprintVariants"" 
                WHERE ""Color"" IS NULL OR ""Color"" = '' OR ""Size"" IS NULL OR ""Size"" = ''";
            var rows = await _dbConnection.QueryAsync<(int BlueprintId, int PrintProviderId)>(query);
            return rows;
        }

        public async Task<(IEnumerable<(string Key, int MaxCount)> Keys, int MaxKeys)> GetDistinctOptionKeysAsync()
        {
            const string query = @"SELECT ""Options"" FROM public.""PrintifyBlueprintVariants""";
            var options = await _dbConnection.QueryAsync<string>(query);
            var keyMax = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int maxKeys = 0;
            foreach (var option in options)
            {
                if (string.IsNullOrWhiteSpace(option)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(option);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        int keyCount = 0;
                        foreach (var property in doc.RootElement.EnumerateObject())
                        {
                            keyCount++;
                            var key = property.Name;
                            int count = property.Value.ValueKind == JsonValueKind.Array
                                ? property.Value.GetArrayLength()
                                : 1;
                            if (keyMax.TryGetValue(key, out var current))
                            {
                                if (count > current) keyMax[key] = count;
                            }
                            else
                            {
                                keyMax[key] = count;
                            }
                        }
                        if (keyCount > maxKeys) maxKeys = keyCount;
                    }
                }
                catch { }
            }
            var keys = keyMax.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => (k.Key, k.Value));
            return (keys, maxKeys);
        }
    }
}
