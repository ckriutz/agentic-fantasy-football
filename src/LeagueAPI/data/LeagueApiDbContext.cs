using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Data;

public sealed class LeagueApiDbContext(DbContextOptions<LeagueApiDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();

    public DbSet<SleeperSyncRun> SleeperSyncRuns => Set<SleeperSyncRun>();

    public DbSet<SportsDataFantasyPlayerEntity> SportsDataFantasyPlayers => Set<SportsDataFantasyPlayerEntity>();

    public DbSet<SportsDataSyncRun> SportsDataSyncRuns => Set<SportsDataSyncRun>();

    public DbSet<YahooSyncRun> YahooSyncRuns => Set<YahooSyncRun>();

    public DbSet<WeeklyPlayerStat> WeeklyPlayerStats => Set<WeeklyPlayerStat>();

    public DbSet<WeeklyPlayerStatValue> WeeklyPlayerStatValues => Set<WeeklyPlayerStatValue>();

    public DbSet<WeeklyPlayerPoint> WeeklyPlayerPoints => Set<WeeklyPlayerPoint>();

    public DbSet<ScoringTemplate> ScoringTemplates => Set<ScoringTemplate>();

    public DbSet<ScoringTemplateRule> ScoringTemplateRules => Set<ScoringTemplateRule>();

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

        modelBuilder.Entity<YahooSyncRun>(entity =>
        {
            entity.ToTable("yahoo_sync_runs");
            entity.HasKey(syncRun => syncRun.SyncRunId);

            entity.Property(syncRun => syncRun.GameKey).HasMaxLength(20);
            entity.Property(syncRun => syncRun.Status).HasMaxLength(32);

            entity.HasIndex(syncRun => new { syncRun.GameKey, syncRun.Season, syncRun.Week });
            entity.HasIndex(syncRun => syncRun.StartedAtUtc);
        });

        modelBuilder.Entity<WeeklyPlayerStat>(entity =>
        {
            entity.ToTable("weekly_player_stats");
            entity.HasKey(playerStat => playerStat.WeeklyPlayerStatId);

            entity.Property(playerStat => playerStat.GameKey).HasMaxLength(20);
            entity.Property(playerStat => playerStat.SleeperPlayerId).HasMaxLength(50);
            entity.Property(playerStat => playerStat.FullName).HasMaxLength(200);
            entity.Property(playerStat => playerStat.Team).HasMaxLength(50);
            entity.Property(playerStat => playerStat.Position).HasMaxLength(20);
            entity.Property(playerStat => playerStat.EditorialTeamAbbr).HasMaxLength(20);

            entity.HasIndex(playerStat => new { playerStat.Season, playerStat.Week, playerStat.YahooPlayerId })
                .IsUnique();
            entity.HasIndex(playerStat => new { playerStat.Season, playerStat.Week, playerStat.Position });
            entity.HasIndex(playerStat => playerStat.SleeperPlayerId);
            entity.HasIndex(playerStat => playerStat.SyncRunId);

            entity.HasOne(playerStat => playerStat.SyncRun)
                .WithMany(syncRun => syncRun.WeeklyPlayerStats)
                .HasForeignKey(playerStat => playerStat.SyncRunId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WeeklyPlayerStatValue>(entity =>
        {
            entity.ToTable("weekly_player_stat_values");
            entity.HasKey(statValue => new { statValue.WeeklyPlayerStatId, statValue.StatId });

            entity.Property(statValue => statValue.StatName).HasMaxLength(100);
            entity.Property(statValue => statValue.Value).HasPrecision(18, 4);

            entity.HasOne(statValue => statValue.WeeklyPlayerStat)
                .WithMany(playerStat => playerStat.StatValues)
                .HasForeignKey(statValue => statValue.WeeklyPlayerStatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(statValue => statValue.StatId);
        });

        modelBuilder.Entity<WeeklyPlayerPoint>(entity =>
        {
            entity.ToTable("weekly_player_points");
            entity.HasKey(playerPoint => playerPoint.WeeklyPlayerPointId);

            entity.Property(playerPoint => playerPoint.TemplateKey).HasMaxLength(100);
            entity.Property(playerPoint => playerPoint.FantasyPoints).HasPrecision(18, 4);

            entity.HasOne(playerPoint => playerPoint.WeeklyPlayerStat)
                .WithMany(playerStat => playerStat.Points)
                .HasForeignKey(playerPoint => playerPoint.WeeklyPlayerStatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(playerPoint => playerPoint.ScoringTemplate)
                .WithMany(template => template.WeeklyPlayerPoints)
                .HasForeignKey(playerPoint => playerPoint.TemplateKey)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(playerPoint => new { playerPoint.WeeklyPlayerStatId, playerPoint.TemplateKey })
                .IsUnique();
            entity.HasIndex(playerPoint => playerPoint.TemplateKey);
        });

        modelBuilder.Entity<ScoringTemplate>(entity =>
        {
            entity.ToTable("scoring_templates");
            entity.HasKey(template => template.TemplateKey);

            entity.Property(template => template.TemplateKey).HasMaxLength(100);
            entity.Property(template => template.Name).HasMaxLength(200);
            entity.Property(template => template.Description).HasMaxLength(1000);

            entity.HasIndex(template => template.IsActive);
        });

        modelBuilder.Entity<ScoringTemplateRule>(entity =>
        {
            entity.ToTable("scoring_template_rules");
            entity.HasKey(rule => new { rule.TemplateKey, rule.StatId });

            entity.Property(rule => rule.TemplateKey).HasMaxLength(100);
            entity.Property(rule => rule.StatName).HasMaxLength(100);
            entity.Property(rule => rule.Modifier).HasPrecision(18, 4);

            entity.HasOne(rule => rule.ScoringTemplate)
                .WithMany(template => template.Rules)
                .HasForeignKey(rule => rule.TemplateKey)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
