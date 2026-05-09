# Testing

The solution includes an xUnit test project:

```text
CourseInventory.Tests
```

## What is tested

- `AccessService`: visitor, owner, shared user, and admin access rules.
- `CustomIdService`: default preview, sequence generation, and invalid pattern validation.
- `StatsService`: item count, total likes, numeric aggregates, and frequent text values.
- `ItemService`: business validation for required serial number, duplicate serial number, and allowed status values.
- Basic security integration tests: anonymous users are redirected away from protected pages, and private details are not exposed.

## Run tests

Use local workspace folders for NuGet and CLI data to avoid depending on the Windows user profile:

```powershell
cd D:\Camp\COURSE_PROJECT
$env:DOTNET_CLI_HOME='D:\Camp\COURSE_PROJECT\.dotnet'
$env:NUGET_PACKAGES='D:\Camp\COURSE_PROJECT\.nuget\packages'
$env:APPDATA='D:\Camp\COURSE_PROJECT\.appdata'
$env:USERPROFILE='D:\Camp\COURSE_PROJECT'
dotnet restore
dotnet test CourseInventory.Tests\CourseInventory.Tests.csproj
```

## Future tests

Useful next tests:

- authenticated integration tests with a custom test auth handler;
- admin role access tests;
- AntiForgeryToken failure tests on POST actions;
- import CSV validation tests when import is added;
- API tests if REST endpoints are added.
