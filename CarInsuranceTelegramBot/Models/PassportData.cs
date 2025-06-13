using System.ComponentModel.DataAnnotations;

namespace CarInsuranceTelegramBot.Models;

public class PassportData
{
    public string? FullName { get; set; }
    
    [Key]
    public string? PassportNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime ExpiryDate { get; set; }

    public string GetAllData()
    {
        return $"Full Name: {FullName}\n" +
            $"Passport Number: {PassportNumber}\n" +
            $"Date of Birth: {DateOfBirth.Date:d}\n" +
            $"Issue Date: {IssueDate.Date:d}\n" +
            $"Expire date: {ExpiryDate.Date:d}";
    }
}
