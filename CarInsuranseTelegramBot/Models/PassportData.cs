namespace CarInsuranseTelegramBot.Models;

public class PassportData
{
    public string? FullName { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }

    public string GetAllData()
    {
        return $"Full Name: {FullName}\n" +
            $"Passport Number: {PassportNumber}\n" +
            $"Date of Birth: {DateOfBirth}\n" +
            $"Issue Date: {IssueDate}\n" +
            $"Expire date: {ExpiryDate}";
    }
}
