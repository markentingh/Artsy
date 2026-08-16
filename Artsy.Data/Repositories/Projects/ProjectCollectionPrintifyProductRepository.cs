using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionPrintifyProductRepository : IProjectCollectionPrintifyProductRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionPrintifyProductRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<ProjectCollectionPrintifyProduct?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionPrintifyProducts"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionPrintifyProduct>(query, new { id });
        }

        public async Task<ProjectCollectionPrintifyProduct?> GetByProductIdAsync(Guid productId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionPrintifyProducts"" WHERE ""ProductId"" = @productId AND ""Status"" = 1";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionPrintifyProduct>(query, new { productId });
        }

        public async Task<ProjectCollectionPrintifyProduct?> GetByPrintifyProductIdAsync(string printifyProductId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionPrintifyProducts"" WHERE ""PrintifyProductId"" = @printifyProductId AND ""Status"" = 1";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionPrintifyProduct>(query, new { printifyProductId });
        }

        public async Task<ProjectCollectionPrintifyProduct?> GetByCollectionAndProductIdAsync(Guid collectionId, Guid productId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionPrintifyProducts"" WHERE ""CollectionId"" = @collectionId AND ""ProductId"" = @productId AND ""Status"" = 1";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionPrintifyProduct>(query, new { collectionId, productId });
        }

        public async Task<IEnumerable<ProjectCollectionPrintifyProduct>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionPrintifyProducts"" WHERE ""CollectionId"" = @collectionId AND ""Status"" = 1 ORDER BY ""Created"" DESC";
            return await _dbConnection.QueryAsync<ProjectCollectionPrintifyProduct>(query, new { collectionId });
        }

        public async Task<ProjectCollectionPrintifyProduct> CreateAsync(ProjectCollectionPrintifyProduct product)
        {
            product.Id = Guid.NewGuid();
            product.Created = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""ProjectCollectionPrintifyProducts"" (""Id"", ""ProjectId"", ""CollectionId"", ""ProductId"", ""PrintifyProductId"", ""PrintifyShopId"", ""PrintifyUserId"", ""ProviderId"", ""Published"", ""Status"", ""RequestJson"", ""ResponseJson"", ""Created"")
                VALUES (@Id, @ProjectId, @CollectionId, @ProductId, @PrintifyProductId, @PrintifyShopId, @PrintifyUserId, @ProviderId, @Published, @Status, @RequestJson, @ResponseJson, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionPrintifyProduct>(query, product);
        }

        public async Task UpdateAsync(ProjectCollectionPrintifyProduct product)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionPrintifyProducts"" SET
                    ""PrintifyProductId"" = @PrintifyProductId,
                    ""PrintifyShopId"" = @PrintifyShopId,
                    ""PrintifyUserId"" = @PrintifyUserId,
                    ""ProviderId"" = @ProviderId,
                    ""Published"" = @Published,
                    ""Status"" = @Status,
                    ""RequestJson"" = @RequestJson,
                    ""ResponseJson"" = @ResponseJson
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, product);
        }

        public async Task SetPublishedAsync(Guid id, bool published)
        {
            const string query = @"UPDATE public.""ProjectCollectionPrintifyProducts"" SET ""Published"" = @published WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id, published });
        }

        public async Task DeleteAsync(Guid id)
        {
            const string query = @"UPDATE public.""ProjectCollectionPrintifyProducts"" SET ""Status"" = 0 WHERE ""Id"" = @id";
            await _dbConnection.ExecuteAsync(query, new { id });
        }
    }
}
