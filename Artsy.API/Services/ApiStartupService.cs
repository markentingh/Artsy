using Artsy.Data.Services;
using Microsoft.AspNetCore.Builder;

namespace Artsy.API.Services
{
    public static class ApiStartupService
    {
        public static void AddApiStartupService(this WebApplicationBuilder builder)
        {
            builder.AddDapperStartupService();
        }
    }
}
