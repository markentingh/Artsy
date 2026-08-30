using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Interfaces.Projects;
using Dapper;

namespace Artsy.Data.Repositories.Projects
{
    public class ProjectCollectionArtworkPlacementRepository : IProjectCollectionArtworkPlacementRepository
    {
        private readonly IDbConnection _dbConnection;

        public ProjectCollectionArtworkPlacementRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<ProjectCollectionArtworkPlacement>> GetByArtworkIdAsync(Guid collectionArtworkId)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionArtworkPlacements""
                WHERE ""CollectionArtworkId"" = @collectionArtworkId ORDER BY ""Index""";
            return await _dbConnection.QueryAsync<ProjectCollectionArtworkPlacement>(query, new { collectionArtworkId });
        }

        public async Task<ProjectCollectionArtworkPlacement?> GetByArtworkIdAndIndexAsync(Guid collectionArtworkId, int index)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionArtworkPlacements""
                WHERE ""CollectionArtworkId"" = @collectionArtworkId AND ""Index"" = @index AND ""GroupId"" IS NULL";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionArtworkPlacement>(query, new { collectionArtworkId, index });
        }

        public async Task<ProjectCollectionArtworkPlacement?> GetByArtworkIdGroupAndPositionAsync(Guid collectionArtworkId, Guid groupId, string position)
        {
            const string query = @"SELECT * FROM public.""ProjectCollectionArtworkPlacements""
                WHERE ""CollectionArtworkId"" = @collectionArtworkId AND ""GroupId"" = @groupId AND ""Position"" = @position";
            return await _dbConnection.QueryFirstOrDefaultAsync<ProjectCollectionArtworkPlacement>(query, new { collectionArtworkId, groupId, position });
        }

        public async Task<ProjectCollectionArtworkPlacement> CreateAsync(ProjectCollectionArtworkPlacement placement)
        {
            if (placement.Id == Guid.Empty) placement.Id = Guid.NewGuid();
            const string query = @"
                INSERT INTO public.""ProjectCollectionArtworkPlacements"" (""Id"", ""CollectionArtworkId"", ""Width"", ""Height"", ""Index"", ""FullSize"", ""PrintifyImageId"", ""ResponseId"", ""GroupId"", ""Position"", ""OptionalPrompt"")
                VALUES (@Id, @CollectionArtworkId, @Width, @Height, @Index, @FullSize, @PrintifyImageId, @ResponseId, @GroupId, @Position, @OptionalPrompt)
                RETURNING *";
            return await _dbConnection.QueryFirstAsync<ProjectCollectionArtworkPlacement>(query, placement);
        }

        public async Task UpdateAsync(ProjectCollectionArtworkPlacement placement)
        {
            const string query = @"
                UPDATE public.""ProjectCollectionArtworkPlacements""
                SET ""Width"" = @Width, ""Height"" = @Height, ""Index"" = @Index, ""FullSize"" = @FullSize, ""PrintifyImageId"" = @PrintifyImageId, ""ResponseId"" = @ResponseId, ""GroupId"" = @GroupId, ""Position"" = @Position, ""OptionalPrompt"" = @OptionalPrompt
                WHERE ""Id"" = @Id";
            await _dbConnection.ExecuteAsync(query, placement);
        }

        public async Task DeleteByArtworkIdAsync(Guid collectionArtworkId)
        {
            const string query = @"DELETE FROM public.""ProjectCollectionArtworkPlacements""
                WHERE ""CollectionArtworkId"" = @collectionArtworkId";
            await _dbConnection.ExecuteAsync(query, new { collectionArtworkId });
        }

        public async Task SetPrintifyImageIdAsync(Guid placementId, string printifyImageId)
        {
            const string query = @"UPDATE public.""ProjectCollectionArtworkPlacements""
                SET ""PrintifyImageId"" = @printifyImageId WHERE ""Id"" = @placementId";
            await _dbConnection.ExecuteAsync(query, new { placementId, printifyImageId });
        }

        public async Task SetFullSizeAsync(Guid placementId, bool fullSize)
        {
            const string query = @"UPDATE public.""ProjectCollectionArtworkPlacements""
                SET ""FullSize"" = @fullSize WHERE ""Id"" = @placementId";
            await _dbConnection.ExecuteAsync(query, new { placementId, fullSize });
        }

        public async Task SetOptionalPromptAsync(Guid placementId, string optionalPrompt)
        {
            const string query = @"UPDATE public.""ProjectCollectionArtworkPlacements""
                SET ""OptionalPrompt"" = @optionalPrompt WHERE ""Id"" = @placementId";
            await _dbConnection.ExecuteAsync(query, new { placementId, optionalPrompt });
        }

        public async Task SetOptionalPromptByGroupAsync(Guid collectionArtworkId, Guid groupId, string optionalPrompt)
        {
            const string query = @"UPDATE public.""ProjectCollectionArtworkPlacements""
                SET ""OptionalPrompt"" = @optionalPrompt WHERE ""CollectionArtworkId"" = @collectionArtworkId AND ""GroupId"" = @groupId";
            await _dbConnection.ExecuteAsync(query, new { collectionArtworkId, groupId, optionalPrompt });
        }
    }
}
