#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/nightly/sdk:9.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/nightly/sdk:9.0-preview AS build
WORKDIR /src
COPY ["CoffeeWeb.csproj", "."]
RUN dotnet restore "./CoffeeWeb.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "CoffeeWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CoffeeWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CoffeeWeb.dll"]