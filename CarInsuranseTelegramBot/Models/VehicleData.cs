namespace CarInsuranseTelegramBot.Models;

public class VehicleData
{
    public required string VIN { get; set; }
    public required string Make { get; set; }
    public required string Model { get; set; }
    public required int Year { get; set; }
    public required string RegistrationNumber { get; set; }

    public string GetAllData()
    {
        return $"VIN: {VIN}\n" +
            $"Make: {Make}\n" +
            $"Model: {Model}\n" +
            $"Year: {Year}\n" +
            $"Registration Number: {RegistrationNumber}";
    }
}
