using Dapper;
using System.Data;
using Artsy.Data.Entities.Projects;
using Artsy.Data.Entities;
using Artsy.Data.Interfaces.Projects;

namespace Artsy.Data.Repositories.Projects
{
    public class CollectionWizardRepository : ICollectionWizardRepository
    {
        readonly IDbConnection _dbConnection;

        public CollectionWizardRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<CollectionWizardData> LoadAsync(Guid projectId, Guid? collectionId = null)
        {
            var hasCollection = collectionId.HasValue && collectionId.Value != Guid.Empty;
            var colId = hasCollection ? collectionId.Value : Guid.Empty;

            // Single SQL query with multiple result sets.
            // Project-level data is always loaded; collection-level data is conditionally included.
            var sql = @"
                -- 1. Questions
                SELECT * FROM public.""ProjectQuestions"" WHERE ""ProjectId"" = @projectId AND ""Status"" = 1 ORDER BY ""Index"";

                -- 2. Items
                SELECT i.""Id"", i.""ProjectId"", i.""Index"", i.""Title"", i.""SocialMedia"",
                    0 AS ""ProductCount"",
                    (SELECT COUNT(*) FROM public.""ProjectItemQuestions"" q WHERE q.""ItemId"" = i.""Id"") AS ""QuestionCount""
                FROM public.""ProjectItems"" i
                WHERE i.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY i.""Index"";

                -- 3. Item Artwork
                SELECT a.* FROM public.""ProjectItemArtwork"" a
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = a.""ItemId""
                WHERE a.""ProjectId"" = @projectId AND i.""Status"" = 1;

                -- 4. Reference Thumbnails
                SELECT r.""Id"", r.""ItemId"" FROM public.""ProjectItemReferences"" r
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = r.""ItemId""
                WHERE r.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY r.""Created"";

                -- 5. Preview Thumbnails
                SELECT p.""Id"", p.""ItemId"" FROM public.""ProjectItemPreviews"" p
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = p.""ItemId""
                WHERE p.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY p.""Created"" DESC;

                -- 6. Item References
                SELECT r.* FROM public.""ProjectItemReferences"" r
                INNER JOIN public.""ProjectItems"" i ON i.""Id"" = r.""ItemId""
                WHERE r.""ProjectId"" = @projectId AND i.""Status"" = 1
                ORDER BY r.""Created"";

                -- 7. Blueprints
                SELECT b.""Id"", b.""BlueprintId"", b.""Name"", b.""BlueprintJson"", b.""PlacementJson"", b.""Prompt"",
                    b.""Description"", b.""SafetyInfo"", b.""PricingJson"", b.""PrintProviderId"",
                    COALESCE(p.""ImageCount"", 0) AS ""ImageCount""
                FROM public.""ProjectBlueprints"" b
                LEFT JOIN public.""PrintifyBlueprints"" p ON p.""BlueprintId"" = b.""BlueprintId""
                WHERE b.""ProjectId"" = @projectId AND b.""Status"" = 1
                ORDER BY b.""Name"";

                -- 8. Blueprint Product Images
                SELECT pi.* FROM public.""ProjectBlueprintProductImages"" pi
                INNER JOIN public.""ProjectBlueprints"" b ON b.""Id"" = pi.""ProjectBlueprintId""
                WHERE b.""ProjectId"" = @projectId AND b.""Status"" = 1;

                -- 9. Printify Blueprint Images (for all blueprint IDs in this project)
                SELECT pi.* FROM public.""PrintifyBlueprintImages"" pi
                WHERE pi.""BlueprintId"" IN (
                    SELECT DISTINCT b.""BlueprintId"" FROM public.""ProjectBlueprints"" b
                    WHERE b.""ProjectId"" = @projectId AND b.""Status"" = 1 AND b.""BlueprintId"" > 0
                );

                -- 10. Printify Blueprint Image Variants
                SELECT v.* FROM public.""PrintifyBlueprintImageVariants"" v
                WHERE v.""BlueprintImageId"" IN (
                    SELECT pi.""Id"" FROM public.""PrintifyBlueprintImages"" pi
                    WHERE pi.""BlueprintId"" IN (
                        SELECT DISTINCT b.""BlueprintId"" FROM public.""ProjectBlueprints"" b
                        WHERE b.""ProjectId"" = @projectId AND b.""Status"" = 1 AND b.""BlueprintId"" > 0
                    )
                );

                -- 11. Printify Blueprint Variants
                SELECT v.* FROM public.""PrintifyBlueprintVariants"" v
                WHERE v.""BlueprintId"" IN (
                    SELECT DISTINCT b.""BlueprintId"" FROM public.""ProjectBlueprints"" b
                    WHERE b.""ProjectId"" = @projectId AND b.""Status"" = 1 AND b.""BlueprintId"" > 0
                );
            ";

            if (hasCollection)
            {
                sql += @"
                -- 12. Collection Answers
                SELECT * FROM public.""ProjectCollectionAnswers"" WHERE ""CollectionId"" = @colId;

                -- 13. Collection Artwork
                SELECT * FROM public.""ProjectCollectionArtwork"" WHERE ""CollectionId"" = @colId AND ""Active"" = true;

                -- 14. Collection Artwork Placements
                SELECT p.* FROM public.""ProjectCollectionArtworkPlacements"" p
                INNER JOIN public.""ProjectCollectionArtwork"" a ON a.""Id"" = p.""CollectionArtworkId""
                WHERE a.""CollectionId"" = @colId AND a.""Active"" = true;

                -- 15. Collection Printify Products
                SELECT * FROM public.""ProjectCollectionPrintifyProducts"" WHERE ""CollectionId"" = @colId;

                -- 16. Collection Mockups
                SELECT * FROM public.""ProjectCollectionPrintifyProductMockups"" WHERE ""CollectionId"" = @colId;

                -- 17. Instagram Posts
                SELECT * FROM public.""ProjectCollectionInstagramPosts"" WHERE ""CollectionId"" = @colId;

                -- 18. Collection Products
                SELECT * FROM public.""ProjectCollectionProducts"" WHERE ""CollectionId"" = @colId;

                -- 19. Collection Product Images
                SELECT * FROM public.""ProjectCollectionProductImages"" WHERE ""CollectionId"" = @colId;
                ";
            }

            var parameters = new { projectId, colId };
            using var multi = await _dbConnection.QueryMultipleAsync(sql, parameters);

            var data = new CollectionWizardData
            {
                Questions = (await multi.ReadAsync<ProjectQuestion>()).ToList(),
                Items = (await multi.ReadAsync<ProjectItemListDto>()).ToList(),
                ItemArtwork = (await multi.ReadAsync<ProjectItemArtwork>()).ToList(),
                RefThumbnails = (await multi.ReadAsync<ProjectItemThumbnailDto>()).ToList(),
                PreviewThumbnails = (await multi.ReadAsync<ProjectItemThumbnailDto>()).ToList(),
                ItemReferences = (await multi.ReadAsync<ProjectItemReference>()).ToList(),
                Blueprints = (await multi.ReadAsync<ProjectBlueprintListDto>()).ToList(),
                BlueprintProductImages = (await multi.ReadAsync<ProjectBlueprintProductImage>()).ToList(),
                PrintifyImages = (await multi.ReadAsync<PrintifyBlueprintImage>()).ToList(),
                PrintifyImageVariants = (await multi.ReadAsync<PrintifyBlueprintImageVariant>()).ToList(),
                PrintifyVariants = (await multi.ReadAsync<PrintifyBlueprintVariant>()).ToList(),
            };

            if (hasCollection)
            {
                data.Answers = (await multi.ReadAsync<ProjectCollectionAnswer>()).ToList();
                data.Artwork = (await multi.ReadAsync<ProjectCollectionArtwork>()).ToList();
                data.ArtworkPlacements = (await multi.ReadAsync<ProjectCollectionArtworkPlacement>()).ToList();
                data.PrintifyProducts = (await multi.ReadAsync<ProjectCollectionPrintifyProduct>()).ToList();
                data.Mockups = (await multi.ReadAsync<ProjectCollectionPrintifyProductMockup>()).ToList();
                data.InstagramPosts = (await multi.ReadAsync<ProjectCollectionInstagramPost>()).ToList();
                data.CollectionProducts = (await multi.ReadAsync<ProjectCollectionProduct>()).ToList();
                data.ProductImages = (await multi.ReadAsync<ProjectCollectionProductImage>()).ToList();
            }

            return data;
        }
    }
}
