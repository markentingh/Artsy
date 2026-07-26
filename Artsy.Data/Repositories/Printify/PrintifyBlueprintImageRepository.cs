using Dapper;
using System.Data;
using System.Text.Json;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces;

namespace Artsy.Data.Repositories
{
    public class PrintifyBlueprintImageRepository : IPrintifyBlueprintImageRepository
    {
        readonly IDbConnection _dbConnection;
        readonly IPrintifyBlueprintImageVariantRepository _imageVariantRepo;

        public PrintifyBlueprintImageRepository(IDbConnection dbConnection, IPrintifyBlueprintImageVariantRepository imageVariantRepo)
        {
            _dbConnection = dbConnection;
            _imageVariantRepo = imageVariantRepo;
        }

        public async Task<IEnumerable<PrintifyBlueprintImage>> GetByBlueprintIdAsync(int blueprintId)
        {
            const string query = @"SELECT * FROM public.""PrintifyBlueprintImages"" WHERE ""BlueprintId"" = @blueprintId ORDER BY ""ImageIndex""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImage>(query, new { blueprintId });
        }

        public async Task<IEnumerable<PrintifyBlueprintImage>> GetByBlueprintIdsAsync(IEnumerable<int> blueprintIds)
        {
            var ids = blueprintIds.ToList();
            if (ids.Count == 0) return Enumerable.Empty<PrintifyBlueprintImage>();
            const string query = @"SELECT * FROM public.""PrintifyBlueprintImages"" WHERE ""BlueprintId"" = ANY(@blueprintIds) ORDER BY ""BlueprintId"", ""ImageIndex""";
            return await _dbConnection.QueryAsync<PrintifyBlueprintImage>(query, new { blueprintIds = ids.ToArray() });
        }

        public async Task<Guid> UpsertAsync(PrintifyBlueprintImage image)
        {
            const string query = @"
                INSERT INTO public.""PrintifyBlueprintImages"" (""BlueprintId"", ""ImageIndex"", ""Variants"", ""Type"", ""Position"", ""DateCreated"", ""DateUpdated"")
                VALUES (@BlueprintId, @ImageIndex, @Variants, @Type, @Position, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (""BlueprintId"", ""ImageIndex"")
                DO UPDATE SET
                    ""Variants"" = @Variants,
                    ""Type"" = @Type,
                    ""Position"" = @Position,
                    ""DateUpdated"" = CURRENT_TIMESTAMP
                RETURNING ""Id""";
            return await _dbConnection.ExecuteScalarAsync<Guid>(query, image);
        }

        public async Task<int> ConvertImageVariantsAsync()
        {
            const string selectQuery = @"SELECT ""Id"", ""Variants"" FROM public.""PrintifyBlueprintImages"" WHERE ""Variants"" != '[]'";
            var rows = (await _dbConnection.QueryAsync<(Guid Id, string Variants)>(selectQuery)).ToList();

            int inserted = 0;
            foreach (var row in rows)
            {
                try
                {
                    var variantIds = JsonSerializer.Deserialize<int[]>(row.Variants ?? "[]") ?? Array.Empty<int>();
                    if (variantIds.Length > 0)
                    {
                        var imageVariants = variantIds.Select(vid => new PrintifyBlueprintImageVariant
                        {
                            ImageId = row.Id,
                            VariantId = vid
                        });
                        await _imageVariantRepo.InsertBatchAsync(imageVariants);
                        inserted += variantIds.Length;
                    }
                }
                catch { }
            }

            return inserted;
        }
    }
}
