using Hangfire.Dashboard;

namespace Artsy.Web.Server.Authorization
{
    public class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            return context.GetHttpContext().User.IsInRole("admin");
        }
    }
}
