namespace CarInsuranceTelegramBot.Models;

public class PolicyData
{
    public required string PolicyNumber { get; set; }
    public required DateTime IssueDate { get; set; }
    public required DateTime ValidUntil { get; set; }
    public required decimal Price { get; set; } = 100m;
    public required string PassportInfo { get; set; }
    public required string VehicleInfo { get; set; }

    public override string ToString()
    {
        return $"Policy Number: {PolicyNumber}\n" +
            $"Issue Date: {IssueDate}\n" +
            $"Valid Until: {ValidUntil}\n" +
            $"Price: {Price}\n\n" +
            $"Passport Info: {PassportInfo}\n\n" +
            $"Vehicle Info: {PassportInfo}";
    }
}