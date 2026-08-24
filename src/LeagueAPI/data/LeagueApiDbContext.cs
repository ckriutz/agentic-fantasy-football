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

    public DbSet<RosterAssignmentEntity> RosterAssignments => Set<RosterAssignmentEntity>();

    public DbSet<DecisionEntity> Decisions => Set<DecisionEntity>();

    public DbSet<AgentProfile> AgentProfiles => Set<AgentProfile>();

    public DbSet<LeagueStateEntity> LeagueState => Set<LeagueStateEntity>();

    public DbSet<PlayoffSettingsEntity> PlayoffSettings => Set<PlayoffSettingsEntity>();

    public DbSet<MatchupEntity> Matchups => Set<MatchupEntity>();

    public DbSet<PlayoffBracketEntity> PlayoffBrackets => Set<PlayoffBracketEntity>();

    public DbSet<PlayoffSeedEntity> PlayoffSeeds => Set<PlayoffSeedEntity>();

    public DbSet<PlayoffBracketGameEntity> PlayoffBracketGames => Set<PlayoffBracketGameEntity>();

    public DbSet<WeeklyRosterSnapshot> WeeklyRosterSnapshots => Set<WeeklyRosterSnapshot>();

    public DbSet<WaiverPriorityEntity> WaiverPriorities => Set<WaiverPriorityEntity>();

    public DbSet<WaiverClaimEntity> WaiverClaims => Set<WaiverClaimEntity>();

    public DbSet<WaiverProcessRunEntity> WaiverProcessRuns => Set<WaiverProcessRunEntity>();

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
            entity.ToTable("league_state", table =>
            {
                table.HasCheckConstraint(
                    "CK_league_state_season_stage",
                    "\"SeasonStage\" IN ('draft', 'regular_season', 'playoffs', 'complete')");
            });
            entity.HasKey(state => state.Id);

            entity.Property(state => state.Id).ValueGeneratedNever();
            entity.Property(state => state.Phase).HasMaxLength(32);
            entity.Property(state => state.SeasonStage).IsRequired().HasMaxLength(32).HasDefaultValue(LeagueStateDefaults.DefaultSeasonStage);
            entity.Property(state => state.UpdatedBy).HasMaxLength(32);

            entity.HasData(new LeagueStateEntity
            {
                Id = LeagueStateDefaults.SingletonId,
                Season = LeagueStateDefaults.DefaultSeason,
                Week = LeagueStateDefaults.PreseasonWeek,
                Phase = LeagueStateDefaults.DefaultPhase,
                SeasonStage = LeagueStateDefaults.DefaultSeasonStage,
                UpdatedAtUtc = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
                UpdatedBy = LeagueStateDefaults.DefaultUpdatedBy
            });
        });

        modelBuilder.Entity<PlayoffSettingsEntity>(entity =>
        {
            entity.ToTable("playoff_settings", table =>
            {
                table.HasCheckConstraint(
                    "CK_playoff_settings_tie_resolution",
                    "\"PlayoffTieResolution\" IN ('higher_seed')");
            });
            entity.HasKey(settings => settings.Id);

            entity.Property(settings => settings.Id).ValueGeneratedNever();
            entity.Property(settings => settings.PlayoffTieResolution).IsRequired().HasMaxLength(32);

            entity.HasData(new PlayoffSettingsEntity
            {
                Id = PlayoffSettingsDefaults.SingletonId,
                RegularSeasonEndWeek = PlayoffSettingsDefaults.RegularSeasonEndWeek,
                PlayoffStartWeek = PlayoffSettingsDefaults.PlayoffStartWeek,
                ChampionshipWeek = PlayoffSettingsDefaults.ChampionshipWeek,
                PlayoffTeamCount = PlayoffSettingsDefaults.PlayoffTeamCount,
                FirstRoundByeCount = PlayoffSettingsDefaults.FirstRoundByeCount,
                Reseed = PlayoffSettingsDefaults.Reseed,
                PlayoffTieResolution = PlayoffSettingsDefaults.PlayoffTieResolution,
                ThirdPlaceGameEnabled = PlayoffSettingsDefaults.ThirdPlaceGameEnabled
            });
        });

        modelBuilder.Entity<MatchupEntity>(entity =>
        {
            entity.ToTable("matchups", table =>
            {
                table.HasCheckConstraint(
                    "CK_matchups_matchup_type",
                    "\"MatchupType\" IN ('regular_season', 'playoff')");
            });
            entity.HasKey(matchup => matchup.Id);

            entity.Property(matchup => matchup.Season).HasDefaultValue(LeagueStateDefaults.DefaultSeason);
            entity.Property(matchup => matchup.MatchupType).IsRequired().HasMaxLength(32).HasDefaultValue(MatchupTypes.RegularSeason);
            entity.Property(matchup => matchup.HomeAgentId).HasMaxLength(100);
            entity.Property(matchup => matchup.AwayAgentId).HasMaxLength(100);
            entity.Property(matchup => matchup.WinnerAgentId).HasMaxLength(100);
            entity.Property(matchup => matchup.HomePoints).HasPrecision(18, 4);
            entity.Property(matchup => matchup.AwayPoints).HasPrecision(18, 4);

            entity.HasIndex(matchup => new { matchup.Season, matchup.Week, matchup.HomeAgentId }).IsUnique();
            entity.HasIndex(matchup => new { matchup.Season, matchup.Week, matchup.AwayAgentId }).IsUnique();
            entity.HasIndex(matchup => new { matchup.Season, matchup.Week, matchup.MatchupType });
        });

        modelBuilder.Entity<PlayoffBracketEntity>(entity =>
        {
            entity.ToTable("playoff_brackets", table =>
            {
                table.HasCheckConstraint(
                    "CK_playoff_brackets_status",
                    "\"Status\" IN ('projected', 'locked', 'complete')");
            });
            entity.HasKey(bracket => bracket.Id);

            entity.Property(bracket => bracket.Status).IsRequired().HasMaxLength(32);

            entity.HasIndex(bracket => bracket.Season).IsUnique();
        });

        modelBuilder.Entity<PlayoffSeedEntity>(entity =>
        {
            entity.ToTable("playoff_seeds");
            entity.HasKey(seed => seed.Id);

            entity.Property(seed => seed.AgentId).IsRequired().HasMaxLength(100);
            entity.Property(seed => seed.WinningPercentage).HasPrecision(18, 4);
            entity.Property(seed => seed.PointsFor).HasPrecision(18, 4);
            entity.Property(seed => seed.PointsAgainst).HasPrecision(18, 4);

            entity.HasIndex(seed => new { seed.BracketId, seed.Seed }).IsUnique();
            entity.HasIndex(seed => new { seed.BracketId, seed.AgentId }).IsUnique();

            entity.HasOne<PlayoffBracketEntity>()
                .WithMany()
                .HasForeignKey(seed => seed.BracketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayoffBracketGameEntity>(entity =>
        {
            entity.ToTable("playoff_bracket_games", table =>
            {
                table.HasCheckConstraint(
                    "CK_playoff_bracket_games_round",
                    "\"Round\" IN ('wild_card', 'semifinal', 'championship', 'third_place')");
                table.HasCheckConstraint(
                    "CK_playoff_bracket_games_status",
                    "\"Status\" IN ('pending', 'scheduled', 'complete')");
                table.HasCheckConstraint(
                    "CK_playoff_bracket_games_home_source_outcome",
                    "\"HomeSourceOutcome\" IS NULL OR \"HomeSourceOutcome\" IN ('winner', 'loser')");
                table.HasCheckConstraint(
                    "CK_playoff_bracket_games_away_source_outcome",
                    "\"AwaySourceOutcome\" IS NULL OR \"AwaySourceOutcome\" IN ('winner', 'loser')");
            });
            entity.HasKey(game => game.Id);

            entity.Property(game => game.Round).IsRequired().HasMaxLength(32);
            entity.Property(game => game.HomeAgentId).HasMaxLength(100);
            entity.Property(game => game.AwayAgentId).HasMaxLength(100);
            entity.Property(game => game.HomeSourceOutcome).HasMaxLength(16);
            entity.Property(game => game.AwaySourceOutcome).HasMaxLength(16);
            entity.Property(game => game.WinnerAgentId).HasMaxLength(100);
            entity.Property(game => game.LoserAgentId).HasMaxLength(100);
            entity.Property(game => game.Status).IsRequired().HasMaxLength(32);

            entity.HasIndex(game => new { game.BracketId, game.Round, game.GameSlot }).IsUnique();
            entity.HasIndex(game => game.MatchupId).IsUnique();

            entity.HasOne<PlayoffBracketEntity>()
                .WithMany()
                .HasForeignKey(game => game.BracketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<MatchupEntity>()
                .WithMany()
                .HasForeignKey(game => game.MatchupId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne<PlayoffBracketGameEntity>()
                .WithMany()
                .HasForeignKey(game => game.HomeSourceGameId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<PlayoffBracketGameEntity>()
                .WithMany()
                .HasForeignKey(game => game.AwaySourceGameId)
                .OnDelete(DeleteBehavior.Restrict);
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
