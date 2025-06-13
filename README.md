# Telegram Car Insurance Bot

This repository contains a .NET 8 implementation of a Telegram bot that guides users through purchasing car insurance. It uses Mindee’s custom OCR endpoints to extract data from passport and vehicle registration photos, and is deployable to AWS Lambda via Function URLs.

## 📦 Setup Instructions and Dependencies

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- Telegram Bot token (obtained from [@BotFather](https://t.me/BotFather))
- Mindee API key (for passport & vehicle endpoints)
- AWS account with permissions to create Lambda functions

### Configuration

1. **Clone repository**

   ```bash
   git clone https://github.com/YourUser/YourRepo.git
   cd YourRepo
   ```

2. **Environment variables**
   Create a file named `.env` in the project root (this is loaded by `dotenv.net`):

   ```dotenv
   TELEGRAM_BOT_TOKEN=your-telegram-token
   MINDEE_API_KEY=sk_live-your-mindee-api-key
   ```

3. **Restore packages**

   ```bash
   dotnet restore
   ```

## 🔄 Detailed Bot Workflow

1. **/start**

   - Clears any existing session; creates a new `UserSession` in state `AwaitingPassport`.
   - Replies: “👋 Welcome! Please send your passport photo to begin.”

2. **Passport Photo Submission**

   - When state is `AwaitingPassport`, handles photo upload.
   - Downloads the image, calls Mindee’s passport OCR, parses `PassportData`.
   - Transitions to `ConfirmingPassport`, sends extracted data with inline buttons:

     - ✅ Confirm
     - 🔄 Retry
     - ✍️ Enter Manually

3. **Passport Confirmation**

   - **Confirm** → state = `AwaitingVehicleFront`, prompt for vehicle front photo.
   - **Retry** → state = `AwaitingPassport`, ask to resend photo.
   - **Enter Manually** → state = `ManualPassportEntry`, user can type fields in free text.

4. **Vehicle Front Photo Submission**

   - In `AwaitingVehicleFront`, handle front-side upload → call Mindee front endpoint → parse registration number & year → state = `ConfirmingVehicleFront`.
   - Send data with:

     - ✅ Confirm
     - 🔄 Retry
     - ✍️ Enter Manually

5. **Vehicle Front Confirmation**

   - **Confirm** → state = `AwaitingVehicleBack`, prompt for back-side photo.
   - **Retry** → reset to `AwaitingVehicleFront`.
   - **Enter Manually** → state = `ManualVehicleEntry` for full manual entry.

6. **Vehicle Back Photo Submission**

   - In `AwaitingVehicleBack`, handle back-side upload → call Mindee back endpoint → parse VIN, make, model → state = `ConfirmingVehicleBack`.
   - Send data with:

     - ✅ Confirm
     - 🔄 Retry
     - ✍️ Enter Manually

7. **Vehicle Back Confirmation**

   - **Confirm** → state = `AwaitingPriceConfirmation`, bot computes price (e.g., 100 USD) and offers:

     - ✅ Accept
     - ❌ Decline
     - 🔄 Start Over

8. **Price Decision**

   - **Accept** → state = `GeneratingPolicy`, bot generates PDF policy, sends document → state = `Completed`.
   - **Decline** → state remains `AwaitingPriceConfirmation`, offers:

     - ✅ Accept Price
     - 🔄 Start Over

9. **Policy Generation**

   - Collect stored `PassportData` + `VehicleData`, build `PolicyData`.
   - Generate PDF via iText7.
   - Send PDF to user.
   - End session or allow /start to begin anew.

## ☁️ Deployment to AWS Lambda via Function URL

1. **Publish for Lambda**

   ```bash
   dotnet publish -c Release \
     -r linux-x64 \
     --self-contained false \
     -o ./publish
   ```

2. **ZIP the output**

   ```bash
   cd publish
   zip -r ../function.zip .
   ```

3. **Create Lambda function**

   - Runtime: **.NET 8 (C#/PowerShell)**
   - Handler:

     ```
     CarInsuranceTelegramBot::CarInsuranceTelegramBot.LambdaEntryPoint::FunctionHandlerAsync
     ```

   - Upload `function.zip` as code.
   - Assign role with `AWSLambdaBasicExecutionRole`.
   - Set environment variables (`TELEGRAM_BOT_TOKEN`, `MINDEE_API_KEY`).

4. **Enable Function URL**

   - In Console → Lambda → Configuration → Function URL → **Create** (Auth = NONE).
   - Copy the URL.

5. **Set Telegram webhook**

   ```bash
   curl.exe --location --request POST \
     "https://api.telegram.org/bot<YOUR_BOT_TOKEN>/setWebhook" \
     --form "url=https://<lambda-url>/telegram/<YOUR_BOT_TOKEN>"
   ```

6. **Verify**

   - `getWebhookInfo` should show no errors.
   - Send `/start` to bot and watch logs in CloudWatch under `/aws/lambda/<YourFunctionName>`.

## 💬 Example Interaction

```
User: /start
Bot: 👋 Welcome! Please send your passport photo to begin.

User: [Passport Photo]
Bot: Full Name: John Doe
     Passport No: AB1234567
     DOB: 1985-02-14
    ✅ Confirm   🔄 Retry   ✍️ Enter Manually

User: ✅ Confirm
Bot: Great! Now send the front side of your vehicle registration.

User: [Vehicle Front Photo]
Bot: Reg. Number: XYZ-1234
     Year: 2015
    ✅ Confirm   🔄 Retry   ✍️ Enter Manually

User: ✅ Confirm
Bot: Now send the back side of your vehicle registration.

User: [Vehicle Back Photo]
Bot: VIN: 1HGCM82633A004352
     Make: Toyota
     Model: Corolla
    ✅ Confirm   🔄 Retry   ✍️ Enter Manually

User: ✅ Confirm
Bot: Your price is 100 USD. Do you accept?
    ✅ Accept   ❌ Decline   🔄 Start Over

User: ✅ Accept
Bot: Generating your policy...
Bot: [Document: policy_1234ABCD.pdf]
```
