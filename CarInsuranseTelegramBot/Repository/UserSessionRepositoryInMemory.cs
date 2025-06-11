using CarInsuranseTelegramBot.Models;

namespace CarInsuranseTelegramBot.Repository;

public class UserSessionRepositoryInMemory : IUserSessionRepository
{
    private readonly UserSessionDbContext _db;
    private ILogger _logger;

    public UserSessionRepositoryInMemory(UserSessionDbContext db, ILogger logger)
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
        }
        _logger.LogWarning("Try to delete non-existent session for chatId: {chatId}", chatId);
    }

    public async Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default)
        => await _db.UserSessions.FindAsync(chatId, ct);

    public async Task UpdateAsync(UserSession session, CancellationToken ct = default)
    {
        var existingSession = await GetAsync(session.ChatId, ct);
        if (existingSession != null)
        {
            _db.UserSessions.Update(session);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Updated session for chatId: {chatId}", existingSession.ChatId);

        }
        _logger.LogWarning("Try to update non-existent session for chatId: {chatId}", session.ChatId);
    }
}