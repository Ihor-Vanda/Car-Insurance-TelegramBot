using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarInsuranceTelegramBot.Services;

public class CountryConfig
{
    public string EndpointNameFront { get; set; } = default!;
    public string EndpointNameBack { get; set; } = default!;
    public bool HasBackPage { get; set; }
}