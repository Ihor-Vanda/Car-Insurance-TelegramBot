using CarInsuranceTelegramBot.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsuranceTelegramBot.Repository;

public class UserSessionDbContext : DbContext
{
    public UserSessionDbContext(DbContextOptions<UserSessionDbContext> opts) : base(opts) { }

    public DbSet<UserSession> UserSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<UserSession>(session =>
        {
            session.OwnsOne(u => u.PassportData);
            session.OwnsOne(u => u.VehicleData);
        });
        builder.Entity<UserSession>().HasKey(x => x.ChatId);
        base.OnModelCreating(builder);
    }

}
