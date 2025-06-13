using System.ComponentModel.DataAnnotations;

namespace CarInsuranceTelegramBot.Models;

public class VehicleData
{
    public string? VIN { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string RegistrationNumber { get; set; }

    public string GetAllData()
    {
        return $"VIN: {VIN}\n" +
            $"Make: {Make}\n" +
            $"Model: {Model}\n" +
            $"Year: {Year}\n" +
            $"Registration Number: {RegistrationNumber}";
    }
}
