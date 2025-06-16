using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CarInsuranceTelegramBot.Models;

namespace CarInsuranceTelegramBot.BotSettings;

public static class BotMessages
{
    // General Messages
    public static string StartMessage =>
        "👋 Hi there! I'm your car insurance bot. \n" +
        "To get started, please send me a clear photo of your *passport*. 🛂";

    public static string ErrorOccurred =>
        "Oops! 😟 Something went wrong. Please try again, or use /start to begin a new session.";

    public static string UnrecognizedMessage =>
        "I didn't quite understand that. 🤔 Please use /start to begin or follow the instructions provided.";

    public static string UnrecognizedCommand =>
        "I don't recognize that command. 🚫 Use /start to begin a new session.";

    public static string PolicyAlreadyIssued =>
        "Your insurance policy has already been issued. 🎉 Use /start to purchase a new policy.";

    public static string InvalidActionContext =>
        "This action isn't valid right now. ❌ Please follow the options I provide.";

    public static string PhotoProcessingFailed =>
        "I couldn't process that photo. 📸 Please try again with a clearer image.";

    public static string MissingPolicyDataError =>
        "Sorry, I'm missing some data to generate your policy. 😔 Please start over with /start.";

    public static string PolicyGenerationSuccess(string policyNumber) =>
        $"🎉 Your insurance policy *#{policyNumber}* has been issued! \n" +
        "Thank you for choosing us. Use /start if you'd like to purchase another policy.";

    public static string DeclinedPriceOffer =>
        "Unfortunately, we don't have different pricing options available at this time. 🤷‍♀️";


    // Passport Messages
    public static string AwaitingPassportPhoto =>
        "Let's try again. Please send a clear photo of your *passport*. 🛂";

    public static string ManualPassportDataPrompt =>
        "Please enter your passport data in the following format: \n\n" +
        "*Full Name;Passport Number;Date Of Birth;Issue Date;Expiry Date*\n\n" +
        "Example date format: `31.12.2000`";

    public static string PassportDataFormatError =>
        "❌ Wrong format! Please ensure you follow this example:\n\n" +
        "*Full Name;Passport Number;Passport Date Of Birth;Date Of Issue;Date Of Expire*\n\n" +
        "Date format example: `31.12.2000`";

    public static string PassportDateParseError =>
        "Can't read the date. 🗓️ Please ensure the date is in `dd.MM.yyyy` format and try again.";

    public static string PassportDataExtracted(PassportData passportData) =>
        $"Here's the passport information I extracted: \n" +
        $"👤 *Full Name*: {passportData.FullName}\n" +
        $"🆔 *Passport Number*: {passportData.PassportNumber}\n" +
        $"🎂 *Date of Birth*: {passportData.DateOfBirth.Date.ToString("d")}\n" +
        $" issuance: {passportData.IssueDate.ToString("d")}\n" +
        $" validity: {passportData.ExpiryDate.ToString("d")}\n\n" +
        "Is this correct?";

    public static string PassportDataConfirmationPrompt(PassportData passportData) =>
        PassportDataExtracted(passportData);

    public static string PassportExtractionFailed =>
        "I couldn't extract data from your passport photo. What would you like to do? 👇";

    public static string PassportConfirmedPromptVehicleFront =>
        "Excellent! Passport confirmed. ✅ Now, choose the country for vehicle documents";

    // Vehicle Messages
    public static string AwaitingVehicleFrontPhoto =>
        "Please send a clear photo of the *front side* of your vehicle registration. 📸";

    public static string VehicleFrontPhotoExtractionFailed =>
        "I couldn't extract data from the vehicle front photo. What's next? 👇";

    public static string VehicleFrontDataExtracted(string registrationNumber, int year) =>
        $"Here's what I found from the front of your vehicle document: \n" +
        $"Registration Number: *{registrationNumber}*\n" +
        $"Year: *{year}*\n\n" +
        "Is this correct?";

    public static string VehicleFrontConfirmedPromptVehicleBack =>
        "Great! Front side confirmed. ✅ Now, please send a clear photo of the *back side* of your vehicle registration document. 🔙";

    public static string AwaitingVehicleBackPhoto =>
        "Please send a clear photo of the *back side* of your vehicle registration. 📸";

    public static string VehicleBackPhotoExtractionFailed =>
        "I couldn't extract data from the vehicle back photo. What's next? 👇";

    public static string VehicleBackDataExtracted(string vin, string model, string make) =>
        $"Here's what I found from the back of your vehicle document: \n" +
        $"VIN: *{vin}*\n" +
        $"Model: *{model}*\n" +
        $"Make: *{make}*\n\n" +
        "Is this correct?";

    public static string ManualVehicleDataPrompt =>
        "Please enter your vehicle data in the following format: \\nn" +
        "*VIN;Make;Model;Year;Registration number*";

    public static string ManualVehicleDataFormatError =>
        "❌ Wrong input! Please follow this example:\n\n" +
        "*VIN;Make;Model;Year;Registration number*";

    public static string VehicleYearParseError =>
        "Can't read the year. 🗓️ Please ensure the year is a valid number and try again following the example:\n\n" +
        "*VIN;Make;Model;Year;Registration number*";

    public static string VehicleDataConfirmationPrompt(VehicleData vehicleData) =>
        $"Here's the vehicle document information: \n" +
        $"🚗 *VIN*: {vehicleData.VIN}\n" +
        $"🛠️ *Make*: {vehicleData.Make}\n" +
        $"🏎️ *Model*: {vehicleData.Model}\n" +
        $"📅 *Year*: {vehicleData.Year}\n" +
        $"🏷️ *Registration Number*: {vehicleData.RegistrationNumber}\n\n" +
        "Is this correct?";


    // Price Confirmation
    public static string PriceConfirmationPrompt(decimal price) =>
        $"Your vehicle insurance price is *{price:C}*. Do you accept this offer? 💰";
}