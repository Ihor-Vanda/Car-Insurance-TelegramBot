using CarInsuranceTelegramBot.Models;

namespace CarInsuranceTelegramBot.Repository;

public interface IUserSessionRepository
{
    Task<UserSession?> GetAsync(long chatId, CancellationToken ct = default);
    Task AddAsync(UserSession session, CancellationToken ct = default);
    Task UpdateAsync(UserSession session, CancellationToken ct = default);
    Task DeleteAsync(long chatId, CancellationToken ct = default);
}