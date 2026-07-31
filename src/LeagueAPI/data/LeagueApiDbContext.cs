using LeagueAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace LeagueAPI.Data;

public sealed class LeagueApiDbContext(DbContextOptions<LeagueApiDbContext> options) : DbContext(options)
{
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();

    public DbSet<SleeperSyncRun> SleeperSyncRuns => Set<SleeperSyncRun>();

    public DbSet<SportsDataFantasyPlayerEntity> SportsDataFantasyPlayers => Set<SportsDataFantasyPlayerEntity>();

    public DbSet<SportsDataSyncRun> SportsDataSyncRuns => Set<SportsDataSyncRun>();

    public DbSet<FantasyProsRankingPlayerEntity> FantasyProsRankingPlayers => Set<FantasyProsRankingPlayerEntity>();

    public DbSet<FantasyProsSyncRun> FantasyProsSyncRuns => Set<FantasyProsSyncRun>();

    public DbSet<WeeklyPlayerScoreEntity> WeeklyPlayerScores => Set<WeeklyPlayerScoreEntity>();

    public DbSet<FantasyProsScoreSyncRun> FantasyProsScoreSyncRuns => Set<FantasyProsScoreSyncRun>();

    public DbSet<YahooSyncRun> YahooSyncRuns => Set<YahooSyncRun>();

    public DbSet<WeeklyPlayerStat> WeeklyPlayerStats => Set<WeeklyPlayerStat>();

    public DbSet<WeeklyPlayerStatValue> WeeklyPlayerStatValues => Set<WeeklyPlayerStatValue>();

    public DbSet<WeeklyPlayerPoint> WeeklyPlayerPoints => Set<WeeklyPlayerPoint>();

    public DbSet<ScoringTemplate> ScoringTemplates => Set<ScoringTemplate>();

    public DbSet<ScoringTemplateRule> ScoringTemplateRules => Set<ScoringTemplateRule>();

    public DbSet<RosterAssignmentEntity> RosterAssignments => Set<RosterAssignmentEntity>();

    public DbSet<DecisionEntity> Decisions => Set<DecisionEntity>();

    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();

    public DbSet<LeagueStateEntity> LeagueState => Set<LeagueStateEntity>();

    public DbSet<MatchupEntity> Matchups => Set<MatchupEntity>();

    public DbSet<WeeklyRosterSnapshot> WeeklyRosterSnapshots => Set<WeeklyRosterSnapshot>();

    public DbSet<YahooOAuthStateEntity> YahooOAuthStates => Set<YahooOAuthStateEntity>();

    public DbSet<WaiverPriorityEntity> WaiverPriorities => Set<WaiverPriorityEntity>();

    public DbSet<WaiverClaimEntity> WaiverClaims => Set<WaiverClaimEntity>();

    public DbSet<WaiverProcessRunEntity> WaiverProcessRuns => Set<WaiverProcessRunEntity>();

    public DbSet<YahooPlayerIdOverrideEntity> YahooPlayerIdOverrides => Set<YahooPlayerIdOverrideEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(player => player.SleeperPlayerId);

            entity.Property(player => player.SleeperPlayerId).HasMaxLength(50);
            entity.Property(player => player.SportradarId).HasMaxLength(50);
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
            entity.Property(player => player.PlayerOwnedAverage).HasPrecision(18, 4);
            entity.Property(player => player.RankAverage).HasMaxLength(32);
            entity.Property(player => player.PositionRank).HasMaxLength(20);

            entity.HasIndex(player => player.YahooId);
            entity.HasIndex(player => player.FantasyDataId);
            entity.HasIndex(player => player.SportradarId);
            entity.HasIndex(player => player.SearchFullNameNormalized);
            entity.HasIndex(player => new { player.TeamAbbr, player.Position });
            entity.HasIndex(player => player.ByeWeek);
        });

        modelBuilder.Entity<SleeperSyncRun>(entity =>
        {
            entity.ToTable("sleeper_sync_runs");
            entity.HasKey(syncRun => syncRun.SyncRunId);

            entity.Property(syncRun => syncRun.ContainerName).HasMaxLength(63);
            entity.Property(syncRun => syncRun.BlobName).HasMaxLength(1024);
            entity.Property(syncRun => syncRun.BlobETag).HasMaxLength(128);
            entity.Property(syncRun => syncRun.ContentHash).HasMaxLength(64);
            entity.Property(syncRun => syncRun.Status).HasMaxLength(32);
            entity.Property(syncRun => syncRun.ErrorMessage).HasColumnType("text");

            entity.HasIndex(syncRun => new { syncRun.ContainerName, syncRun.BlobName, syncRun.RetrievedAtUtc });
            entity.HasIndex(syncRun => syncRun.StartedAtUtc);
            entity.HasIndex(syncRun => syncRun.ContentHash);
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

            entity.Property(syncRun => syncRun.ContainerName).HasMaxLength(63);
            entity.Property(syncRun => syncRun.BlobName).HasMaxLength(1024);
            entity.Property(syncRun => syncRun.BlobETag).HasMaxLength(128);
            entity.Property(syncRun => syncRun.ContentHash).HasMaxLength(64);
            entity.Property(syncRun => syncRun.Status).HasMaxLength(32);
            entity.Property(syncRun => syncRun.ErrorMessage).HasColumnType("text");

            entity.HasIndex(syncRun => new { syncRun.ContainerName, syncRun.BlobName, syncRun.RetrievedAtUtc });
            entity.HasIndex(syncRun => syncRun.StartedAtUtc);
            entity.HasIndex(syncRun => syncRun.ContentHash);
        });

        modelBuilder.Entity<FantasyProsRankingPlayerEntity>(entity =>
        {
            entity.ToTable("fantasypros_ranking_players");
            entity.HasKey(player => player.PlayerId);

            entity.Property(player => player.PlayerId).ValueGeneratedNever();
            entity.Property(player => player.PlayerName).HasMaxLength(200);
            entity.Property(player => player.SportsDataId).HasMaxLength(50);
            entity.Property(player => player.PlayerTeamId).HasMaxLength(20);
            entity.Property(player => player.PlayerPositionId).HasMaxLength(20);
            entity.Property(player => player.PlayerPositions).HasMaxLength(100);
            entity.Property(player => player.PlayerShortName).HasMaxLength(100);
            entity.Property(player => player.PlayerEligibility).HasMaxLength(100);
            entity.Property(player => player.PlayerYahooPositions).HasMaxLength(100);
            entity.Property(player => player.PlayerPageUrl).HasMaxLength(2048);
            entity.Property(player => player.PlayerFilename).HasMaxLength(255);
            entity.Property(player => player.PlayerYahooId).HasMaxLength(50);
            entity.Property(player => player.CbsPlayerId).HasMaxLength(50);
            entity.Property(player => player.PlayerByeWeek).HasMaxLength(10);
            entity.Property(player => player.PlayerOwnedAverage).HasPrecision(18, 4);
            entity.Property(player => player.PlayerOwnedEspn).HasPrecision(18, 4);
            entity.Property(player => player.PlayerOwnedYahoo).HasPrecision(18, 4);
            entity.Property(player => player.PlayerEcrDelta).HasPrecision(18, 4);
            entity.Property(player => player.RankMinimum).HasMaxLength(32);
            entity.Property(player => player.RankMaximum).HasMaxLength(32);
            entity.Property(player => player.RankAverage).HasMaxLength(32);
            entity.Property(player => player.RankStandardDeviation).HasMaxLength(32);
            entity.Property(player => player.PositionRank).HasMaxLength(20);
            entity.Property(player => player.RawJson).HasColumnType("text");

            entity.HasIndex(player => player.SportsDataId);
            entity.HasIndex(player => player.PlayerYahooId);
            entity.HasIndex(player => new { player.Season, player.Week });
        });

        modelBuilder.Entity<FantasyProsSyncRun>(entity =>
        {
            entity.ToTable("fantasypros_sync_runs");
            entity.HasKey(syncRun => syncRun.SyncRunId);

            entity.Property(syncRun => syncRun.ContainerName).HasMaxLength(63);
            entity.Property(syncRun => syncRun.BlobName).HasMaxLength(1024);
            entity.Property(syncRun => syncRun.BlobETag).HasMaxLength(128);
            entity.Property(syncRun => syncRun.ContentHash).HasMaxLength(64);
            entity.Property(syncRun => syncRun.Status).HasMaxLength(32);
            entity.Property(syncRun => syncRun.ErrorMessage).HasColumnType("text");

            entity.HasIndex(syncRun => new { syncRun.ContainerName, syncRun.BlobName, syncRun.Season, syncRun.Week, syncRun.RetrievedAtUtc });
            entity.HasIndex(syncRun => syncRun.StartedAtUtc);
            entity.HasIndex(syncRun => syncRun.ContentHash);
        });

        modelBuilder.Entity<WeeklyPlayerScoreEntity>(entity =>
        {
            entity.ToTable("weekly_player_scores");
            entity.HasKey(score => new { score.Season, score.Week, score.FantasyProsPlayerId });

            entity.Property(score => score.SleeperPlayerId).HasMaxLength(50);
            entity.Property(score => score.PlayerName).HasMaxLength(200);
            entity.Property(score => score.PositionId).HasMaxLength(20);
            entity.Property(score => score.TeamId).HasMaxLength(20);
            entity.Property(score => score.Points).HasPrecision(8, 2);

            entity.HasIndex(score => score.SleeperPlayerId);
            entity.HasIndex(score => new { score.Season, score.Week });
            entity.HasIndex(score => score.SyncRunId);
        });

        modelBuilder.Entity<FantasyProsScoreSyncRun>(entity =>
        {
            entity.ToTable("fantasypros_score_sync_runs");
            entity.HasKey(syncRun => syncRun.SyncRunId);

            entity.Property(syncRun => syncRun.ContainerName).HasMaxLength(63);
            entity.Property(syncRun => syncRun.BlobName).HasMaxLength(1024);
            entity.Property(syncRun => syncRun.BlobETag).HasMaxLength(128);
            entity.Property(syncRun => syncRun.ContentHash).HasMaxLength(64);
            entity.Property(syncRun => syncRun.Status).HasMaxLength(32);
            entity.Property(syncRun => syncRun.ServedSeason).HasMaxLength(20);
            entity.Property(syncRun => syncRun.ServedScoring).HasMaxLength(20);
            entity.Property(syncRun => syncRun.ErrorMessage).HasColumnType("text");

            entity.HasIndex(syncRun => new { syncRun.ContainerName, syncRun.BlobName, syncRun.Season, syncRun.EndWeek, syncRun.ContentHash });
            entity.HasIndex(syncRun => syncRun.StartedAtUtc);
            entity.HasIndex(syncRun => syncRun.ContentHash);
            entity.HasIndex(syncRun => syncRun.Season);
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

        modelBuilder.Entity<RosterAssignmentEntity>(entity =>
        {
            entity.ToTable("roster_assignments");
            entity.HasKey(assignment => assignment.RosterAssignmentId);

            entity.Property(assignment => assignment.AgentId).HasMaxLength(100);
            entity.Property(assignment => assignment.SleeperPlayerId).HasMaxLength(50);
            entity.Property(assignment => assignment.AcquisitionSource).HasMaxLength(32);
            entity.Property(assignment => assignment.SlotType).IsRequired().HasMaxLength(8).HasDefaultValue("BN");

            entity.HasIndex(assignment => assignment.AgentId);
            entity.HasIndex(assignment => assignment.SleeperPlayerId).IsUnique();
            entity.HasIndex(assignment => new { assignment.AgentId, assignment.SlotType })
                .IsUnique()
                .HasFilter("\"SlotType\" <> 'BN'");

            entity.HasOne<PlayerEntity>()
                .WithMany()
                .HasForeignKey(assignment => assignment.SleeperPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DecisionEntity>(entity =>
        {
            entity.ToTable("decisions");
            entity.HasKey(decision => decision.DecisionId);

            entity.Property(decision => decision.AgentId).HasMaxLength(100);
            entity.Property(decision => decision.Type).HasMaxLength(50);
            entity.Property(decision => decision.Reasoning).HasColumnType("text");
            entity.Property(decision => decision.Action).HasColumnType("text");

            entity.HasIndex(decision => decision.AgentId);
            entity.HasIndex(decision => decision.CreatedAtUtc);
        });

        modelBuilder.Entity<AgentProfile>(entity =>
        {
            entity.ToTable("agent_profiles");
            entity.HasKey(profile => profile.AgentId);

            entity.Property(profile => profile.AgentId).HasMaxLength(100);
            entity.Property(profile => profile.TeamName).HasMaxLength(200);
            entity.Property(profile => profile.ModelName).HasMaxLength(200);
            entity.Property(profile => profile.Connection).HasMaxLength(50);

            entity.HasIndex(profile => profile.IsEnabled);
        });

        modelBuilder.Entity<LeagueStateEntity>(entity =>
        {
            entity.ToTable("league_state");
            entity.HasKey(state => state.Id);

            entity.Property(state => state.Id).ValueGeneratedNever();
            entity.Property(state => state.Phase).HasMaxLength(32);
            entity.Property(state => state.UpdatedBy).HasMaxLength(32);

            entity.HasData(new LeagueStateEntity
            {
                Id = LeagueStateDefaults.SingletonId,
                Season = LeagueStateDefaults.DefaultSeason,
                Week = LeagueStateDefaults.PreseasonWeek,
                Phase = LeagueStateDefaults.DefaultPhase,
                UpdatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UpdatedBy = LeagueStateDefaults.DefaultUpdatedBy
            });
        });

        modelBuilder.Entity<MatchupEntity>(entity =>
        {
            entity.ToTable("matchups");
            entity.HasKey(matchup => matchup.Id);

            entity.Property(matchup => matchup.HomeAgentId).HasMaxLength(100);
            entity.Property(matchup => matchup.AwayAgentId).HasMaxLength(100);
            entity.Property(matchup => matchup.WinnerAgentId).HasMaxLength(100);
            entity.Property(matchup => matchup.HomePoints).HasPrecision(18, 4);
            entity.Property(matchup => matchup.AwayPoints).HasPrecision(18, 4);

            entity.HasIndex(matchup => new { matchup.Week, matchup.HomeAgentId });
        });

        modelBuilder.Entity<WeeklyRosterSnapshot>(entity =>
        {
            entity.ToTable("weekly_roster_snapshots");
            entity.HasKey(snapshot => snapshot.WeeklyRosterSnapshotId);

            entity.Property(snapshot => snapshot.AgentId).HasMaxLength(100);
            entity.Property(snapshot => snapshot.SleeperPlayerId).HasMaxLength(50);
            entity.Property(snapshot => snapshot.SlotType).HasMaxLength(8);

            entity.HasIndex(snapshot => new
            {
                snapshot.Season,
                snapshot.Week,
                snapshot.AgentId,
                snapshot.SleeperPlayerId
            }).IsUnique();
            entity.HasIndex(snapshot => new { snapshot.Season, snapshot.Week, snapshot.AgentId });
        });

        modelBuilder.Entity<YahooOAuthStateEntity>(entity =>
        {
            entity.ToTable("yahoo_oauth_state");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).ValueGeneratedNever();
            entity.Property(row => row.AccessToken).HasColumnType("text");
            entity.Property(row => row.RefreshToken).HasColumnType("text");
            entity.Property(row => row.TokenType).HasMaxLength(50);
            entity.Property(row => row.Scope).HasMaxLength(500);
            entity.Property(row => row.AuthorizationState).HasMaxLength(128);
        });

        modelBuilder.Entity<YahooPlayerIdOverrideEntity>(entity =>
        {
            entity.ToTable("yahoo_player_id_overrides");
            entity.HasKey(row => row.YahooPlayerId);
            entity.Property(row => row.YahooPlayerId).ValueGeneratedNever();
            entity.Property(row => row.SleeperPlayerId).HasMaxLength(50);
            entity.Property(row => row.Note).HasMaxLength(500);
            entity.HasIndex(row => row.SleeperPlayerId);
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

        modelBuilder.Entity<WaiverPriorityEntity>(entity =>
        {
            entity.ToTable("waiver_priority");
            entity.HasKey(priority => priority.AgentId);

            entity.Property(priority => priority.AgentId).HasMaxLength(100);
            entity.HasIndex(priority => priority.Priority).IsUnique();
        });

        modelBuilder.Entity<WaiverClaimEntity>(entity =>
        {
            entity.ToTable("waiver_claims");
            entity.HasKey(claim => claim.WaiverClaimId);

            entity.Property(claim => claim.AgentId).HasMaxLength(100);
            entity.Property(claim => claim.AddSleeperPlayerId).HasMaxLength(50);
            entity.Property(claim => claim.DropSleeperPlayerId).HasMaxLength(50);
            entity.Property(claim => claim.Status).HasMaxLength(32);
            entity.Property(claim => claim.FailureReason).HasColumnType("text");

            entity.HasIndex(claim => new { claim.Season, claim.Week, claim.Status });
            entity.HasIndex(claim => new { claim.Season, claim.Week, claim.AgentId, claim.ClaimOrder });
        });

        modelBuilder.Entity<WaiverProcessRunEntity>(entity =>
        {
            entity.ToTable("waiver_process_runs");
            entity.HasKey(run => run.WaiverProcessRunId);

            entity.Property(run => run.Status).HasMaxLength(32);
            entity.Property(run => run.ErrorMessage).HasColumnType("text");

            entity.HasIndex(run => new { run.Season, run.Week });
        });
    }
}
