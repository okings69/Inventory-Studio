# Salesforce CRM Integration

Inventory Studio can send a user profile to Salesforce by creating:

1. an `Account` from the submitted company information
2. a `Contact` linked to that account through `Contact.AccountId`

The implementation uses `ISalesforceService`, `HttpClientFactory`, and strongly typed `SalesforceOptions`. Secrets are read from user-secrets locally or environment variables in production.

## 1. Create a Salesforce Developer Org

1. Go to `https://developer.salesforce.com/signup`.
2. Create a free Developer Edition org with an email address you can access.
3. Log in to the org and confirm that you can open Setup.

Salesforce Help also documents Developer Edition sign-up from Trailhead: <https://help.salesforce.com/s/articleView?id=005298906&type=1>.

## 2. Create a Connected App

1. In Salesforce, open Setup.
2. Search for `App Manager`.
3. Choose `New Connected App`.
4. Fill in the app name, API name, and contact email.
5. Enable OAuth settings.
6. Add a callback URL. For this demo flow the callback is not used by the application, but Salesforce requires one when OAuth settings are enabled. Use a local placeholder such as:

   ```text
   https://localhost:5001/signin-salesforce
   ```

7. Add OAuth scopes:
   - `Manage user data via APIs (api)`
   - optionally `Perform requests at any time (refresh_token, offline_access)` if you later replace the demo flow with web server OAuth
8. Save the connected app.
9. Open the app details and copy:
   - `Consumer Key` -> `Salesforce:ClientId`
   - `Consumer Secret` -> `Salesforce:ClientSecret`

Salesforce Connected App OAuth documentation: <https://help.salesforce.com/s/articleView?id=platform.ev_relay_create_connected_app.htm&type=5>.

## 3. Choose LoginUrl and ApiVersion

Use:

```text
Salesforce:LoginUrl=https://login.salesforce.com
Salesforce:ApiVersion=60.0
```

For a sandbox, use:

```text
Salesforce:LoginUrl=https://test.salesforce.com
```

## 4. Demo Authentication Flow

This project uses Salesforce's OAuth username-password flow only for a course demo because it is easy to explain and does not require adding a full OAuth callback workflow to the MVC app.

Required values:

```text
Salesforce:AuthFlow=Password
Salesforce:ClientId
Salesforce:ClientSecret
Salesforce:Username
Salesforce:Password
Salesforce:SecurityToken
Salesforce:LoginUrl
Salesforce:ApiVersion
```

Salesforce recommends using more secure flows, such as web server OAuth with PKCE or client credentials, when possible. Official username-password flow documentation: <https://help.salesforce.com/s/articleView?id=remoteaccess_oauth_username_password_flow.htm&type=5>.

### Recommended fallback for External Client Apps: Client Credentials

If your Salesforce org uses the newer External Client App UI and username-password keeps failing with `invalid_grant`, use the client credentials flow instead. This is usually easier for a server-side ASP.NET Core demo because no Salesforce user's password or security token is stored.

In Salesforce:

1. Open `Setup` -> `App Manager`.
2. Open your `Inventory Studio` external client app.
3. In OAuth settings, enable the client credentials flow.
4. In the app policies, choose an integration user for the flow to run as.
5. Make sure that user can create `Account` and `Contact` records.
6. Copy your org's My Domain URL, for example:

   ```text
   https://your-domain.my.salesforce.com
   ```

For client credentials, configure:

```powershell
dotnet user-secrets set "Salesforce:AuthFlow" "ClientCredentials"
dotnet user-secrets set "Salesforce:ClientId" "YOUR_CONSUMER_KEY"
dotnet user-secrets set "Salesforce:ClientSecret" "YOUR_CONSUMER_SECRET"
dotnet user-secrets set "Salesforce:LoginUrl" "https://your-domain.my.salesforce.com"
dotnet user-secrets set "Salesforce:ApiVersion" "60.0"
```

For production environment variables:

```text
Salesforce__AuthFlow=ClientCredentials
Salesforce__ClientId=YOUR_CONSUMER_KEY
Salesforce__ClientSecret=YOUR_CONSUMER_SECRET
Salesforce__LoginUrl=https://your-domain.my.salesforce.com
Salesforce__ApiVersion=60.0
```

Salesforce notes that client credentials requests must use the org's My Domain URL, not `https://login.salesforce.com`.

## 5. Get the Security Token

1. In Salesforce, open your personal settings.
2. Search for `Reset My Security Token`.
3. Click `Reset Security Token`.
4. Copy the token from the email Salesforce sends you.

Salesforce security token documentation: <https://help.salesforce.com/apex/HTViewHelpDoc?id=user_security_token.htm>.

## 6. Store Secrets Locally

Do not put Salesforce secrets in `appsettings.json`.

From the web project directory:

```powershell
dotnet user-secrets set "Salesforce:AuthFlow" "Password"
dotnet user-secrets set "Salesforce:ClientId" "YOUR_CONSUMER_KEY"
dotnet user-secrets set "Salesforce:ClientSecret" "YOUR_CONSUMER_SECRET"
dotnet user-secrets set "Salesforce:Username" "your-salesforce-user@example.com"
dotnet user-secrets set "Salesforce:Password" "YOUR_SALESFORCE_PASSWORD"
dotnet user-secrets set "Salesforce:SecurityToken" "YOUR_SECURITY_TOKEN"
dotnet user-secrets set "Salesforce:LoginUrl" "https://login.salesforce.com"
dotnet user-secrets set "Salesforce:ApiVersion" "60.0"
```

## 7. Production Environment Variables

Use double underscores for nested configuration:

```text
Salesforce__AuthFlow=Password
Salesforce__ClientId=YOUR_CONSUMER_KEY
Salesforce__ClientSecret=YOUR_CONSUMER_SECRET
Salesforce__Username=your-salesforce-user@example.com
Salesforce__Password=YOUR_SALESFORCE_PASSWORD
Salesforce__SecurityToken=YOUR_SECURITY_TOKEN
Salesforce__LoginUrl=https://login.salesforce.com
Salesforce__ApiVersion=60.0
```

## 8. Demo Flow

1. Start Inventory Studio.
2. Sign in.
3. Open `Profile`.
4. Click `Send profile to Salesforce`.
5. Fill in company, phone, job title, city, country, and notes.
6. Submit the form.
7. In Salesforce, open Accounts and verify that the new Account exists.
8. Open the Account and verify that the Contact is linked.

Admins can call the same action with a user id if needed:

```text
/Profile/Salesforce?userId=USER_ID
```

Non-admin users can submit only their own profile.

## 9. Troubleshooting

- `Salesforce is not configured`: one or more required settings are missing.
- `Salesforce authentication failed`: check client id, client secret, username, password, security token, and LoginUrl.
- `Account could not be created`: confirm the Salesforce user has API access and permission to create Accounts.
- `Contact could not be created`: confirm the Salesforce user has permission to create Contacts and write `AccountId`.
- New Developer Orgs can block the username-password flow. If that happens, use a course demo org where the flow is allowed, or replace the service authentication method with web server OAuth or client credentials.
