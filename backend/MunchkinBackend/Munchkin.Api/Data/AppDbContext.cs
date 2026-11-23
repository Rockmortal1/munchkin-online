using Microsoft.EntityFrameworkCore;
using Munchkin.Api.Models;

namespace Munchkin.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<CardPack> CardPacks => Set<CardPack>();
        public DbSet<Card> Cards => Set<Card>();
        public DbSet<Season> Seasions => Set<Season>();
        public DbSet<Match> Matches => Set<Match>();
        public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //Evitamos usuarios y mails duplicados
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            //Evitar Borrado en cascada
            modelBuilder.Entity<Match>()
                .HasOne(m => m.WinnerUser)
                .WithMany()
                .HasForeignKey(m => m.WinnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
