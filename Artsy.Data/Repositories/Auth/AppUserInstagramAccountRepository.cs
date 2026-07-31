using Dapper;
using System.Data;
using Artsy.Data.Entities.Auth;
using Artsy.Data.Interfaces.Auth;

namespace Artsy.Data.Repositories.Auth
{
    public class AppUserInstagramAccountRepository : IAppUserInstagramAccountRepository
    {
        readonly IDbConnection _dbConnection;

        public AppUserInstagramAccountRepository(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public async Task<IEnumerable<AppUserInstagramAccount>> GetByUserIdAsync(Guid appUserId)
        {
            const string query = @"SELECT * FROM public.""AppUserInstagramAccounts"" WHERE ""AppUserId"" = @appUserId ORDER BY ""DateCreated""";
            return await _dbConnection.QueryAsync<AppUserInstagramAccount>(query, new { appUserId });
        }

        public async Task<AppUserInstagramAccount?> GetByIdAsync(Guid id)
        {
            const string query = @"SELECT * FROM public.""AppUserInstagramAccounts"" WHERE ""Id"" = @id";
            return await _dbConnection.QueryFirstOrDefaultAsync<AppUserInstagramAccount>(query, new { id });
        }

        public async Task<AppUserInstagramAccount?> GetByInstagramBusinessAccountIdAsync(Guid appUserId, string instagramBusinessAccountId)
        {
            const string query = @"SELECT * FROM public.""AppUserInstagramAccounts"" WHERE ""AppUserId"" = @appUserId AND ""InstagramBusinessAccountId"" = @instagramBusinessAccountId";
            return await _dbConnection.QueryFirstOrDefaultAsync<AppUserInstagramAccount>(query, new { appUserId, instagramBusinessAccountId });
        }

        public async Task<AppUserInstagramAccount> UpsertAsync(AppUserInstagramAccount account)
        {
            const string query = @"
                INSERT INTO public.""AppUserInstagramAccounts"" (""Id"", ""AppUserId"", ""InstagramBusinessAccountId"", ""MetaUserId"", ""MetaAccessToken"", ""MetaTokenExpiresAtUtc"", ""Username"", ""ProfilePictureUrl"", ""DateCreated"", ""DateUpdated"")
                VALUES (@Id, @AppUserId, @InstagramBusinessAccountId, @MetaUserId, @MetaAccessToken, @MetaTokenExpiresAtUtc, @Username, @ProfilePictureUrl, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (""AppUserId"", ""InstagramBusinessAccountId"")
                DO UPDATE SET
                    ""MetaUserId"" = @MetaUserId,
                    ""MetaAccessToken"" = @MetaAccessToken,
                    ""MetaTokenExpiresAtUtc"" = @MetaTokenExpiresAtUtc,
                    ""Username"" = @Username,
                    ""ProfilePictureUrl"" = @ProfilePictureUrl,
                    ""DateUpdated"" = CURRENT_TIMESTAMP
                RETURNING *";

            if (account.Id == Guid.Empty)
                account.Id = Guid.NewGuid();

            return await _dbConnection.QuerySingleAsync<AppUserInstagramAccount>(query, account);
        }

        public async Task DeleteAsync(Guid id, Guid appUserId)
        {
            const string query = @"DELETE FROM public.""AppUserInstagramAccounts"" WHERE ""Id"" = @id AND ""AppUserId"" = @appUserId";
            await _dbConnection.ExecuteAsync(query, new { id, appUserId });
        }
    }
}
