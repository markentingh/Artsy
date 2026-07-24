using Artsy.Data.Interfaces;
using Artsy.Data.Interfaces.Auth;
using Artsy.Data.Interfaces.Projects;
using Artsy.Data.Repositories;
using Artsy.Data.Repositories.Auth;
using Artsy.Data.Repositories.Projects;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using System.Data;

namespace Artsy.Data.Services
{
    public static class DapperStartupService
    {
        public static void AddDapperStartupService(this WebApplicationBuilder builder)
        {
            builder.Services.AddTransient<IDbConnection>((sp) => new NpgsqlConnection(builder.Configuration["ConnectionStrings:Database"] ?? ""));

            builder.Services.AddTransient<IAppUserRepository, AppUserRepository>();
            builder.Services.AddTransient<IAppRoleRepository, AppRoleRepository>();
            builder.Services.AddTransient<IAppUserRolesRepository, AppUserRolesRepository>();
            builder.Services.AddTransient<IAppUserTokenRepository, AppUserTokenRepository>();
            builder.Services.AddTransient<IProjectRepository, ProjectRepository>();
            builder.Services.AddTransient<IProjectCollectionRepository, ProjectCollectionRepository>();
            builder.Services.AddTransient<IProjectItemRepository, ProjectItemRepository>();
            builder.Services.AddTransient<IProjectBlueprintsRepository, ProjectBlueprintsRepository>();
            builder.Services.AddTransient<IProjectItemArtworkRepository, ProjectItemArtworkRepository>();
            builder.Services.AddTransient<IProjectItemQuestionRepository, ProjectItemQuestionRepository>();
            builder.Services.AddTransient<IProjectQuestionRepository, ProjectQuestionRepository>();
            builder.Services.AddTransient<IProjectCollectionArtworkRepository, ProjectCollectionArtworkRepository>();
            builder.Services.AddTransient<IProjectCollectionProductImageRepository, ProjectCollectionProductImageRepository>();
            builder.Services.AddTransient<IProjectImageGenerationRepository, ProjectImageGenerationRepository>();
            builder.Services.AddTransient<IProjectImageUpscaleRepository, ProjectImageUpscaleRepository>();
            builder.Services.AddTransient<IProjectCollectionAnswerRepository, ProjectCollectionAnswerRepository>();
            builder.Services.AddTransient<IProjectItemPreviewRepository, ProjectItemPreviewRepository>();
            builder.Services.AddTransient<IProjectItemReferenceRepository, ProjectItemReferenceRepository>();
            builder.Services.AddTransient<ILLMModelsRepository, LLMModelsRepository>();
            builder.Services.AddTransient<IImageGenerationModelRepository, ImageGenerationModelRepository>();
            builder.Services.AddTransient<IPrintifyBlueprintRepository, PrintifyBlueprintRepository>();
            builder.Services.AddTransient<IPrintifyBlueprintPrintProviderRepository, PrintifyBlueprintPrintProviderRepository>();
            builder.Services.AddTransient<IPrintifyBlueprintVariantRepository, PrintifyBlueprintVariantRepository>();
            builder.Services.AddTransient<IPrintifyBlueprintVariantPlaceholderRepository, PrintifyBlueprintVariantPlaceholderRepository>();
            builder.Services.AddTransient<IPrintifyBlueprintShippingRepository, PrintifyBlueprintShippingRepository>();
            builder.Services.AddTransient<IPrintifyBlueprintImageRepository, PrintifyBlueprintImageRepository>();
            builder.Services.AddTransient<ITrendRepository, TrendRepository>();
        }
    }
}
