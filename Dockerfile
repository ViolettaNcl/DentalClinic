# syntax=docker/dockerfile:1

# ============ 1. Build & Publish ============
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Кэшируем restore отдельным слоем: пересобирается только если поменялся .csproj
COPY DentalClinic.csproj ./
RUN dotnet restore ./DentalClinic.csproj

# Теперь копируем всё остальное и публикуем
COPY . .
RUN dotnet publish ./DentalClinic.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============ 2. Runtime ============
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# .NET 10 images already ship with the non-root `app` account (APP_UID=1654).
# Make the published tree and avatar upload directory writable by that account.
COPY --from=build /app/publish .
RUN mkdir -p /app/wwwroot/uploads/avatars \
    && chown -R app:app /app

USER $APP_UID

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DentalClinic.dll"]
