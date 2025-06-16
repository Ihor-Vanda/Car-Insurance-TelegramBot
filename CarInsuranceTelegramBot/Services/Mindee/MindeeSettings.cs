using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarInsuranceTelegramBot.Services;

public class MindeeSettings
{
    public string AccountName = "Ihor17344";
    public string Version = "1";
    public Dictionary<string, CountryConfig> Countries { get; set; } = new();
    public CountryConfig GetForCountry(string code)
    {
        if (Countries.TryGetValue(code, out var cfg))
            return cfg;
        return new CountryConfig
        {
            EndpointNameFront = Countries["Default"].EndpointNameFront,
            EndpointNameBack = Countries["Default"].EndpointNameBack,
            HasBackPage = Countries["Default"].HasBackPage
        };
    }
}