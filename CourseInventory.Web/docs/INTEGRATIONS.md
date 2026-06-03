# Inventory Studio Integrations

## Google Drive Support Tickets

Inventory Studio can create support tickets by uploading one JSON file into a Google Drive folder. A Google Apps Script watches the folder, reads each JSON file, sends the Gmail notification, and renames the file with `PROCESSED_`.

For a normal personal Google Drive account, Inventory Studio uploads with the signed-in Google user's OAuth token. Do not use a service account for a personal `My Drive` folder: service accounts do not have personal Drive storage quota, so uploads can fail with `Service Accounts do not have storage quota`.

### Folder Setup

1. In Google Drive, create or open:

   ```text
   InventoryStudio/SupportTickets
   ```

2. Open the folder in the browser.
3. Copy the folder ID from the URL. In this example, the folder ID is the part after `/folders/`:

   ```text
   https://drive.google.com/drive/folders/FOLDER_ID_HERE
   ```

### Google Cloud OAuth Setup

1. Open Google Cloud Console.
2. Create or select the project used for Inventory Studio integrations.
3. Enable `Google Drive API`.
4. Configure the OAuth consent screen.
5. Add the Google Drive scope used by Inventory Studio:

   ```text
   https://www.googleapis.com/auth/drive.file
   ```

6. Create an OAuth client for the web app:

   ```text
   APIs & Services > Credentials > Create Credentials > OAuth client ID > Web application
   ```

   Use the OAuth Web Client ID, not a service account `client_id`. A correct Google OAuth Web Client ID ends with:

   ```text
   .apps.googleusercontent.com
   ```

7. Add the local redirect URI:

   ```text
   http://localhost:5158/signin-google
   ```

8. Add the production redirect URI:

   ```text
   https://inventory-studio-web.onrender.com/signin-google
   ```

### Local User-Secrets

Set Google login credentials and the Drive folder ID:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "GOOGLE_OAUTH_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "GOOGLE_OAUTH_CLIENT_SECRET"
dotnet user-secrets set "GoogleDrive:SupportTicketsFolderId" "GOOGLE_DRIVE_FOLDER_ID"
```

If you previously pasted values from a service account JSON, replace them. Service account `client_id` values are usually numeric and cause Google to show `The OAuth client was not found` / `invalid_client`.

Restart the app after changing user-secrets:

```powershell
dotnet run --urls http://localhost:5158
```

Then sign out and sign in again with Google. The consent screen must include Google Drive file access so Inventory Studio can store:

```text
access_token
refresh_token
expires_at
```

These tokens are stored server-side through ASP.NET Core Identity user tokens.

### Service Accounts

Service accounts are not the default path for this course project because a normal personal Google Drive folder lives in a user's `My Drive`. Service accounts can work only when you use one of these Google Workspace patterns:

- Shared Drives where the service account has access and quota behavior is appropriate.
- Workspace domain-wide delegation, where the service account impersonates a real domain user.

If you keep a service account key for experiments, do not commit it. For local development, keep it only here:

```text
D:\Camp\COURSE_PROJECT\CourseInventory.Web\.secrets\google-service-account.json
```

Do not use `D:\service-account.json` for credentials. In this project that file has been used for generated support ticket JSON examples, not Google service account keys.

### Render Environment Variables

Use OAuth credentials and the folder ID in Render:

```text
Authentication__Google__ClientId=<google oauth client id>
Authentication__Google__ClientSecret=<google oauth client secret>
GoogleDrive__SupportTicketsFolderId=<google drive folder id>
```

### JSON Uploaded by Inventory Studio

```json
{
  "reportedBy": "Orian",
  "reportedByEmail": "oriangidolcalebou@gmail.com",
  "inventory": "Lab Equipment",
  "link": "https://inventory-studio-web.onrender.com/Inventories/Details/42",
  "priority": "High",
  "summary": "The export page fails when I click Download.",
  "createdAtUtc": "2026-05-31T18:00:00Z"
}
```

File names use:

```text
support-ticket-{yyyyMMdd-HHmmss}-{shortGuid}.json
```

Example:

```text
support-ticket-20260531-180000-a1b2c3d4.json
```

### Apps Script Workflow

1. Apps Script watches `InventoryStudio/SupportTickets`.
2. Inventory Studio uploads a JSON file to that folder.
3. Apps Script reads the JSON content.
4. Apps Script sends the formatted email through Gmail.
5. Apps Script renames the file with `PROCESSED_`.

### Browser Demo Steps

1. Start Inventory Studio.
2. Sign in.
3. Open an inventory details page.
4. Click `Help`.
5. Choose `Low`, `Average`, or `High`.
6. Enter the support request summary.
7. Submit the form.
8. Confirm the app redirects back to the inventory details page with a success message.
9. Open Google Drive as the same Google user and confirm a new JSON file appears in `InventoryStudio/SupportTickets`.
10. Confirm Apps Script sends the Gmail notification and renames the file with `PROCESSED_`.

### Troubleshooting

- `Support ticket upload is not configured yet`: set `GoogleDrive:SupportTicketsFolderId`.
- `Missing Google Drive OAuth token`: sign in with Google again.
- `Google Drive consent required`: sign out, sign in with Google again, and accept the Drive permission.
- `Google Drive folder not found`: check `GoogleDrive:SupportTicketsFolderId`.
- `Google Drive access denied`: sign in as the Google account that owns or can edit the folder.
- Google shows `The OAuth client was not found` / `invalid_client`: `Authentication:Google:ClientId` is not a valid OAuth Web Client ID, or the OAuth client was deleted. Create a Web application OAuth client and update user-secrets. Do not use a service account JSON `client_id`.
- Upload fails with API errors: confirm Google Drive API is enabled in Google Cloud.
- Apps Script does not process the file: confirm the script watches the same folder ID configured in Inventory Studio.
- Never paste OAuth client secrets, refresh tokens, service account JSON, screenshots, Git commits, or regular `appsettings.json`.
