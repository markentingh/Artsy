using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionPrintifyProductMockupRepository : IProjectCollectionPrintifyProductMockupRepository
    {
        readonly IDbConnection _dbConnection;

        public ProjectCollectionPrintifyProductMockupRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectCollectionPrintifyProductMockup>> GetByPrintifyProductIdAsync(Guid printifyProductId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionPrintifyProductMockups"" WHERE ""PrintifyProductId"" = @printifyProductId AND ""Status"" = 1 ORDER BY ""Created""";
            return await _dbConnection.QueryAsync<ProjectCollectionPrintifyProductMockup>(query, new { printifyProductId });
        }

        public async Task<IEnumerable<ProjectCollectionPrintifyProductMockup>> GetByCollectionIdAsync(Guid collectionId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionPrintifyProductMockups"" WHERE ""CollectionId"" = @collectionId AND ""Status"" = 1 ORDER BY ""PrintifyProductId"", ""Created""";
            return await _dbConnection.QueryAsync<ProjectCollectionPrintifyProductMockup>(query, new { collectionId });
        }

        public async Task<ProjectCollectionPrintifyProductMockup> CreateAsync(ProjectCollectionPrintifyProductMockup mockup)
        {
            if (mockup.Id == Guid.Empty)
                mockup.Id = Guid.NewGuid();
            mockup.Created = DateTime.UtcNow;
            const string query = @"
                INSERT INTO public.""ProjectCollectionPrintifyProductMockups"" (""Id"", ""ProjectId"", ""CollectionId"", ""PrintifyProductId"", ""VariantIds"", ""Position"", ""ImageUrl"", ""IsDefault"", ""Status"", ""Created"")
                VALUES (@Id, @ProjectId, @CollectionId, @PrintifyProductId, @VariantIds, @Position, @ImageUrl, @IsDefault, @Status, @Created)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionPrintifyProductMockup>(query, mockup);
        }

        public async Task DeleteByPrintifyProductIdAsync(Guid printifyProductId)
        {
            const string query = @"UPDATE public.""ProjectCollectionPrintifyProductMockups"" SET ""Status"" = 0 WHERE ""PrintifyProductId"" = @printifyProductId";
            await _dbConnection.ExecuteAsync(query, new { printifyProductId });
        }
    }
}
