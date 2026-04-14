using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Data;

public sealed class LeagueApiDbContext(DbContextOptions<LeagueApiDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();

    public DbSet<SleeperSyncRun> SleeperSyncRuns => Set<SleeperSyncRun>();

    public DbSet<SportsDataFantasyPlayerEntity> SportsDataFantasyPlayers => Set<SportsDataFantasyPlayerEntity>();

    public DbSet<SportsDataSyncRun> SportsDataSyncRuns => Set<SportsDataSyncRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(player => player.SleeperPlayerId);

            entity.Property(player => player.SleeperPlayerId).HasMaxLength(50);
            entity.Property(player => player.FullName).HasMaxLength(200);
            entity.Property(player => player.FirstName).HasMaxLength(100);
            entity.Property(player => player.LastName).HasMaxLength(100);
            entity.Property(player => player.SearchFullNameNormalized).HasMaxLength(200);
            entity.Property(player => player.Team).HasMaxLength(50);
            entity.Property(player => player.TeamAbbr).HasMaxLength(50);
            entity.Property(player => player.Position).HasMaxLength(20);
            entity.Property(player => player.FantasyPositionsTokenized).HasMaxLength(100);
            entity.Property(player => player.Status).HasMaxLength(50);
            entity.Property(player => player.Sport).HasMaxLength(20);

            entity.HasIndex(player => player.YahooId);
            entity.HasIndex(player => player.FantasyDataId);
            entity.HasIndex(player => player.SearchFullNameNormalized);
            entity.HasIndex(player => new { player.TeamAbbr, player.Position });
            entity.HasIndex(player => player.ByeWeek);
        });

        modelBuilder.Entity<SleeperSyncRun>(entity =>
        {
            entity.ToTable("sleeper_sync_runs");
            entity.HasKey(syncRun => syncRun.SyncRunId);

            entity.Property(syncRun => syncRun.Status).HasMaxLength(32);
            entity.Property(syncRun => syncRun.SnapshotFileName).HasMaxLength(260);
            entity.Property(syncRun => syncRun.SnapshotRelativePath).HasMaxLength(520);
            entity.Property(syncRun => syncRun.PayloadSha256).HasMaxLength(64);
        });

        modelBuilder.Entity<SportsDataFantasyPlayerEntity>(entity =>
        {
            entity.ToTable("sportsdata_fantasy_players");
            entity.HasKey(player => player.SportsDataPlayerId);

            entity.Property(player => player.SportsDataPlayerId).ValueGeneratedNever();
            entity.Property(player => player.Name).HasMaxLength(200);
            entity.Property(player => player.Team).HasMaxLength(50);
            entity.Property(player => player.Position).HasMaxLength(20);
            entity.Property(player => player.FantasyPlayerKey).HasMaxLength(100);
        });

        modelBuilder.Entity<SportsDataSyncRun>(entity =>
        {
            entity.ToTable("sportsdata_sync_runs");
            entity.HasKey(syncRun => syncRun.SyncRunId);

            entity.Property(syncRun => syncRun.Status).HasMaxLength(32);
        });
    }
}
