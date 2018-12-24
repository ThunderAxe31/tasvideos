using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TASVideos.Data.Entity;
using TASVideos.Data.Entity.Awards;
using TASVideos.Data.Entity.Forum;
using TASVideos.Data.Entity.Game;

namespace TASVideos.Data
{
	public class ApplicationDbContext : IdentityDbContext<User, Role, int, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
	{
		private readonly IHttpContextAccessor _httpContext;

		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
			: base(options)
		{
			_httpContext = httpContextAccessor;
		}

		public DbSet<RolePermission> RolePermission { get; set; }
		public DbSet<WikiPage> WikiPages { get; set; }
		public DbSet<WikiPageReferral> WikiReferrals { get; set; }
		public DbSet<RoleLink> RoleLinks { get; set; }
		public DbSet<Submission> Submissions { get; set; }
		public DbSet<SubmissionAuthor> SubmissionAuthors { get; set; }
		public DbSet<SubmissionStatusHistory> SubmissionStatusHistory { get; set; }
		public DbSet<Tier> Tiers { get; set; }

		public DbSet<Publication> Publications { get; set; }
		public DbSet<PublicationAuthor> PublicationAuthors { get; set; }
		public DbSet<PublicationFile> PublicationFiles { get; set; }
		public DbSet<PublicationTag> PublicationTags { get; set; }
		public DbSet<PublicationRating> PublicationRatings { get; set; }
		public DbSet<PublicationFlag> PublicationFlags { get; set; }

		public DbSet<Tag> Tags { get; set; }
		public DbSet<Flag> Flags { get; set; }

		public DbSet<Award> Awards { get; set; }
		public DbSet<PublicationAward> PublicationAwards { get; set; }
		public DbSet<UserAward> UserAwards { get; set; }

		// Game tables
		public DbSet<Game> Games { get; set; }
		public DbSet<GameGenre> GameGenres { get; set; }
		public DbSet<Genre> Genres { get; set; }
		public DbSet<GameSystem> GameSystems { get; set; }
		public DbSet<GameSystemFrameRate> GameSystemFrameRates { get; set; }
		public DbSet<GameRom> Roms { get; set; }

		// Forum tables
		public DbSet<ForumCategory> ForumCategories { get; set; }
		public DbSet<Forum> Forums { get; set; }
		public DbSet<ForumTopic> ForumTopics { get; set; }
		public DbSet<ForumPost> ForumPosts { get; set; }
		public DbSet<ForumPoll> ForumPolls { get; set; }
		public DbSet<ForumPollOption> ForumPollOptions { get; set; }
		public DbSet<ForumPollOptionVote> ForumPollOptionVotes { get; set; }
		public DbSet<ForumTopicWatch> ForumTopicWatches { get; set; }

		public DbSet<PrivateMessage> PrivateMessages { get; set; }

		public DbSet<DatabaseFile> Files { get; set; }

		// Userfiles
		public DbSet<UserFile> UserFiles { get; set; }
		public DbSet<UserFileComment> UserFileComments { get; set; }

		public DbSet<MediaPost> MediaPosts { get; set;}

		public override int SaveChanges(bool acceptAllChangesOnSuccess)
		{
			PerformTrackingUpdates();

			ChangeTracker.AutoDetectChangesEnabled = false;
			var result = base.SaveChanges(acceptAllChangesOnSuccess);
			ChangeTracker.AutoDetectChangesEnabled = true;

			return result;
		}

		public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			PerformTrackingUpdates();
			return base.SaveChangesAsync(cancellationToken);
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			builder.Entity<ForumTopic>(entity =>
			{
				entity.HasIndex(e => e.PageName)
					.HasName($"{nameof(ForumTopic.PageName)}Index")
					.IsUnique()
					.HasFilter($"([{nameof(ForumTopic.PageName)}] IS NOT NULL)");
			});
			
			builder.Entity<User>(entity =>
			{
				entity.HasIndex(e => e.NormalizedEmail)
					.HasName("EmailIndex");

				entity.HasIndex(e => e.NormalizedUserName)
					.HasName("UserNameIndex")
					.IsUnique()
					.HasFilter($"([{nameof(User.NormalizedUserName)}] IS NOT NULL)");

				entity.HasMany(e => e.SentPrivateMessages)
					.WithOne(e => e.FromUser)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(e => e.ReceivedPrivateMessages)
					.WithOne(e => e.ToUser)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(e => e.UserFiles)
					.WithOne(e => e.Author)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(e => e.UserFileComments)
					.WithOne(e => e.User)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasMany(e => e.ForumTopicWatches)
					.WithOne(e => e.User)
					.OnDelete(DeleteBehavior.Restrict);
			});

			builder.Entity<Tier>(entity =>
			{
				entity.Property(e => e.Id)
					.ValueGeneratedNever()
					.HasAnnotation("DatabaseGenerated", DatabaseGeneratedOption.None);

				entity.HasIndex(e => e.Name)
					.IsUnique()
					.HasFilter($"([{nameof(Tier.Name)}] IS NOT NULL)");
			});

			builder.Entity<UserLogin>(entity =>
			{
				entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });
				entity.HasIndex(e => e.UserId);
			});

			builder.Entity<RoleClaim>(entity =>
			{
				entity.HasIndex(e => e.RoleId);
			});

			builder.Entity<UserClaim>(entity =>
			{
				entity.HasIndex(e => e.UserId);
			});

			builder.Entity<UserRole>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.RoleId });
				entity.HasIndex(e => e.RoleId);
			});

			builder.Entity<UserToken>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });
			});

			builder.Entity<RolePermission>()
				.HasKey(rp => new { rp.RoleId, rp.PermissionId });

			builder.Entity<RolePermission>()
				.HasOne(pt => pt.Role)
				.WithMany(p => p.RolePermission)
				.HasForeignKey(pt => pt.RoleId);

			builder.Entity<WikiPage>(entity =>
			{
				entity.HasIndex(e => new { e.PageName, e.Revision })
					.HasName("PageNameIndex")
					.IsUnique()
					.HasFilter($"([{nameof(WikiPage.PageName)}] IS NOT NULL)");
			});

			builder.Entity<GameSystem>(entity =>
			{
				entity.Property(e => e.Id)
					.ValueGeneratedNever()
					.HasAnnotation("DatabaseGenerated", DatabaseGeneratedOption.None);

				entity.HasIndex(e => e.Code)
					.IsUnique()
					.HasFilter($"([{nameof(GameSystem.Code)}] IS NOT NULL)");
			});

			builder.Entity<GameSystemFrameRate>(entity =>
			{
				entity.HasOne(sf => sf.System)
					.WithMany(s => s.SystemFrameRates)
					.OnDelete(DeleteBehavior.Restrict);
			});

			builder.Entity<GameRom>(entity =>
			{
				entity.HasIndex(e => e.Md5)
					.IsUnique()
					.HasFilter($"([{nameof(GameRom.Sha1)}] IS NOT NULL)");

				entity.HasIndex(e => e.Sha1)
					.IsUnique()
					.HasFilter($"([{nameof(GameRom.Sha1)}] IS NOT NULL)");
			});

			builder.Entity<SubmissionAuthor>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.SubmissionId });
				entity.HasIndex(e => e.SubmissionId);
			});

			builder.Entity<PublicationAuthor>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.PublicationId });
				entity.HasIndex(e => e.PublicationId);
			});

			builder.Entity<Publication>(entity =>
			{
				entity.HasOne(p => p.System)
					.WithMany(s => s.Publications)
					.OnDelete(DeleteBehavior.Restrict);

				entity.HasOne(p => p.Rom)
					.WithMany(r => r.Publications)
					.OnDelete(DeleteBehavior.Restrict);
			});

			builder.Entity<GameGenre>(entity =>
			{
				entity.HasKey(e => new { e.GameId, e.GenreId });
				entity.HasIndex(e => e.GameId);
			});

			builder.Entity<Genre>(entity =>
			{
				entity.Property(e => e.Id)
					.ValueGeneratedNever()
					.HasAnnotation("DatabaseGenerated", DatabaseGeneratedOption.None);
			});

			builder.Entity<Flag>(entity =>
			{
				entity.Property(e => e.Id)
					.ValueGeneratedNever()
					.HasAnnotation("DatabaseGenerated", DatabaseGeneratedOption.None);
			});

			builder.Entity<PublicationTag>(entity =>
			{
				entity.HasKey(e => new { e.PublicationId, e.TagId });
				entity.HasIndex(e => e.PublicationId);
			});

			builder.Entity<PublicationFlag>(entity =>
			{
				entity.HasKey(e => new { e.PublicationId, e.FlagId });
				entity.HasIndex(e => e.PublicationId);
			});

			builder.Entity<PublicationRating>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.PublicationId, e.Type });
				entity.HasIndex(e => e.PublicationId);
				entity.HasIndex(e => new { e.UserId, e.PublicationId, e.Type })
					.IsUnique();
			});

			builder.Entity<Tag>(entity =>
			{
				entity.HasIndex(e => e.Code)
					.IsUnique()
					.HasFilter($"([{nameof(Tag.Code)}] IS NOT NULL)");
			});

			builder.Entity<ForumPoll>(entity =>
			{
				entity.HasOne(p => p.Topic)
					.WithOne(t => t.Poll)
					.HasForeignKey<ForumTopic>(t => t.PollId);
			});

			builder.Entity<Submission>(entity =>
			{
				entity.HasIndex(e => e.Status);
			});

			builder.Entity<UserFile>(entity =>
			{
				entity.HasIndex(e => e.Hidden);
			});

			builder.Entity<PrivateMessage>(entity =>
			{
				entity.HasIndex(e => new { e.ToUserId, e.ReadOn, e.DeletedForToUser });
			});

			builder.Entity<ForumTopicWatch>(entity =>
			{
				entity.HasKey(e => new { e.UserId, e.ForumTopicId });
				entity.HasIndex(e => e.ForumTopicId);
			});
		}

		private void PerformTrackingUpdates()
		{
			ChangeTracker.DetectChanges();

			foreach (var entry in ChangeTracker.Entries()
				.Where(e => e.State == EntityState.Added))
			{
				if (entry.Entity is ITrackable trackable)
				{
					if (trackable.CreateTimeStamp.Year == 1) // Don't set if already set
					{
						trackable.CreateTimeStamp = DateTime.UtcNow;
					}

					if (trackable.LastUpdateTimeStamp.Year == 1)
					{
						trackable.LastUpdateTimeStamp = DateTime.UtcNow;
					}

					if (string.IsNullOrWhiteSpace(trackable.LastUpdateUserName))
					{
						trackable.LastUpdateUserName = _httpContext?.HttpContext?.User?.Identity?.Name;
					}

					if (string.IsNullOrWhiteSpace(trackable.CreateUserName))
					{
						trackable.CreateUserName = _httpContext?.HttpContext?.User?.Identity?.Name;
					}
				}
			}

			foreach (var entry in ChangeTracker.Entries()
				.Where(e => e.State == EntityState.Modified))
			{
				if (entry.Entity is ITrackable trackable)
				{
					if (trackable.LastUpdateTimeStamp.Year == 1) // Don't set if already set
					{
						trackable.LastUpdateTimeStamp = DateTime.UtcNow;
					}

					if (string.IsNullOrWhiteSpace(trackable.LastUpdateUserName))
					{
						trackable.LastUpdateUserName = _httpContext?.HttpContext?.User?.Identity?.Name;
					}
				}
			}
		}
	}
}
