# Coffee Analytics Hub - EQ900 API
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CoffeeApi.csproj", "."]
RUN dotnet restore "CoffeeApi.csproj"
COPY . .
RUN dotnet build "CoffeeApi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "CoffeeApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

# Create directory for SQLite database (will be mounted as volume)
RUN mkdir -p /app/data

COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__Default="Data Source=/app/data/coffee.db"

ENTRYPOINT ["dotnet", "CoffeeApi.dll"]
