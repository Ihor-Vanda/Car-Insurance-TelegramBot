using CarInsuranceTelegramBot.Models;
using Microsoft.EntityFrameworkCore;

namespace CarInsuranceTelegramBot.Repository;

public class UserSessionRepositoryInMemory : IUserSessionRepository
{
    private readonly UserSessionDbContext _db;
    private readonly ILogger<UserSessionRepositoryInMemory> _logger;

    public UserSessionRepositoryInMemory(UserSessionDbContext db, ILogger<UserSessionRepositoryInMemory> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task AddAsync(UserSession session, CancellationToken ct = default)
    {
        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created session for chatId: {chatId}", session.ChatId);
    }

    public async Task DeleteAsync(long chatId, CancellationToken ct = default)
    {
        var existingSession = await GetAsync(chatId, ct);
        if (existingSession != null)
        {
            _db.UserSessions.Remove(existingSession);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Deleted session for chatId: {chatId}", chatId);
            return;
        }
        _logger.LogWarning("Try to delete non-existent session for chatId: {chatId}", chatId);
    }

    public async Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default)
        => await _db.UserSessions.FindAsync(chatId, ct);

    public async Task UpdateAsync(UserSession session, CancellationToken ct = default)
    {
        var existing = await _db.UserSessions
            .Include(s => s.PassportData)
            .Include(s => s.VehicleData)
            .FirstOrDefaultAsync(s => s.ChatId == session.ChatId, ct);

        if (existing == null)
        {
            _logger.LogWarning("Try to update non-existent session for chatId: {chatId}", session.ChatId);
            return;
        }
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(session);

            // --- PassportData ---
            if (session.PassportData != null)
            {
                if (existing.PassportData != null)
                {
                    _db.Entry(existing.PassportData)
                        .CurrentValues
                        .SetValues(session.PassportData);
                }
                else
                {
                    existing.PassportData = session.PassportData;
                }
            }

            // --- VehicleData ---
            if (session.VehicleData != null)
            {
                if (existing.VehicleData != null)
                {
                    _db.Entry(existing.VehicleData)
                        .CurrentValues
                        .SetValues(session.VehicleData);
                }
                else
                {
                    existing.VehicleData = session.VehicleData;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Updated session for chatId: {chatId}", existing.ChatId);
    }
}