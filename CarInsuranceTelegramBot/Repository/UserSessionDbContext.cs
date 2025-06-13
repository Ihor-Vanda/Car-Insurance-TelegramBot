using CarInsuranceTelegramBot.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsuranceTelegramBot.Repository;

public class UserSessionDbContext : DbContext
{
    public UserSessionDbContext(DbContextOptions<UserSessionDbContext> opts) : base(opts) { }

    public DbSet<UserSession> UserSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<UserSession>().HasKey(x => x.ChatId);
        base.OnModelCreating(builder);
    }

}
