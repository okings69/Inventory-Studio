FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY CourseInventory.Web/CourseInventory.Web.csproj CourseInventory.Web/
COPY CourseInventory.Tests/CourseInventory.Tests.csproj CourseInventory.Tests/
COPY CourseInventory.slnx ./
RUN dotnet restore CourseInventory.Web/CourseInventory.Web.csproj

COPY . .
RUN dotnet publish CourseInventory.Web/CourseInventory.Web.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish .

EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet CourseInventory.Web.dll"]
