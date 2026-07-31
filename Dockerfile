# syntax=docker/dockerfile:1

# ============ 1. Build & Publish ============
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
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

# Непривилегированный пользователь вместо root
RUN adduser --disabled-password --gecos "" appuser

COPY --from=build /app/publish .

# Папка для загруженных файлов (аватары) — монтируется как volume в compose,
# поэтому сразу отдаём её в собственность appuser
RUN mkdir -p /app/wwwroot/uploads/avatars \
    && chown -R appuser:appuser /app

USER appuser

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "DentalClinic.dll"]
