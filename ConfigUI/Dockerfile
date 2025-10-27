#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["ConfigUI/ConfigUI.csproj", "ConfigUI/"]
COPY ["TelegramMultiBot.Database/TelegramMultiBot.Database.csproj", "TelegramMultiBot.Database/"]
RUN dotnet restore "./ConfigUI/ConfigUI.csproj"
COPY . .
WORKDIR "/src/ConfigUI"
RUN dotnet build "./ConfigUI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ConfigUI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ConfigUI.dll"]