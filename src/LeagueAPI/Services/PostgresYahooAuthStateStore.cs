using LeagueAPI.Data;
using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Services;

public sealed class PostgresYahooAuthStateStore(
    IDbContextFactory<LeagueApiDbContext> dbContextFactory)
{
    private readonly IDbContextFactory<LeagueApiDbContext> _dbContextFactory = dbContextFactory;

    public async Task<YahooOAuthState> GetStateAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.YahooOAuthStates
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.Id == YahooOAuthStateEntity.SingletonId,
                cancellationToken);

        return entity is null ? new YahooOAuthState() : MapToState(entity);
    }

    public async Task SaveStateAsync(YahooOAuthState state, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.YahooOAuthStates
            .FirstOrDefaultAsync(
                row => row.Id == YahooOAuthStateEntity.SingletonId,
                cancellationToken);

        if (entity is null)
        {
            entity = new YahooOAuthStateEntity { Id = YahooOAuthStateEntity.SingletonId };
            dbContext.YahooOAuthStates.Add(entity);
        }

        entity.AccessToken = state.AccessToken;
        entity.RefreshToken = state.RefreshToken;
        entity.TokenType = state.TokenType;
        entity.ExpiresInSeconds = state.ExpiresInSeconds;
        entity.IssuedAtUtc = state.IssuedAtUtc;
        entity.AccessTokenExpiresAtUtc = state.AccessTokenExpiresAtUtc;
        entity.LastRefreshedAtUtc = state.LastRefreshedAtUtc;
        entity.Scope = state.Scope;
        entity.AuthorizationState = state.AuthorizationState;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static YahooOAuthState MapToState(YahooOAuthStateEntity entity)
    {
        return new YahooOAuthState
        {
            AccessToken = entity.AccessToken,
            RefreshToken = entity.RefreshToken,
            TokenType = entity.TokenType,
            ExpiresInSeconds = entity.ExpiresInSeconds,
            IssuedAtUtc = entity.IssuedAtUtc,
            AccessTokenExpiresAtUtc = entity.AccessTokenExpiresAtUtc,
            LastRefreshedAtUtc = entity.LastRefreshedAtUtc,
            Scope = entity.Scope,
            AuthorizationState = entity.AuthorizationState
        };
    }
}
