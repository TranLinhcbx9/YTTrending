# syntax=docker/dockerfile:1

# Build API
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

# tan dung Docker cache, copy csproj and restore as distinct layers
COPY YTTrending.sln Directory.Build.props Directory.Packages.props ./
COPY src/YTTrending.Domain/YTTrending.Domain.csproj src/YTTrending.Domain/
COPY src/YTTrending.Application/YTTrending.Application.csproj src/YTTrending.Application/
COPY src/YTTrending.Infrastructure/YTTrending.Infrastructure.csproj src/YTTrending.Infrastructure/
COPY src/YTTrending.API/YTTrending.API.csproj src/YTTrending.API/

RUN dotnet restore src/YTTrending.API/YTTrending.API.csproj

COPY src/ src/

RUN dotnet publish src/YTTrending.API/YTTrending.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Build the EF Core migration executable only; migrations are not applied to the database yet
FROM build AS migration-build

RUN dotnet tool install \
    --tool-path /tools \
    dotnet-ef \
    --version 8.0.11

RUN /tools/dotnet-ef migrations bundle \
    --project src/YTTrending.Infrastructure/YTTrending.Infrastructure.csproj \
    --startup-project src/YTTrending.API/YTTrending.API.csproj \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output /app/migrate

# Build the final lightweight runtime image for the ASP.NET Core API
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS api
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "YTTrending.API.dll"]

# Build the migration runner image and apply EF Core migrations when the container starts
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS migrator
WORKDIR /app
COPY --from=migration-build /app/migrate ./migrate
COPY src/YTTrending.API/appsettings.json ./appsettings.json
RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["./migrate"]
