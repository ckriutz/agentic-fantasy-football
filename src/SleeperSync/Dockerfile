# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY SleeperSync.csproj ./
RUN dotnet restore SleeperSync.csproj

COPY . ./
RUN dotnet publish SleeperSync.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SleeperSync.dll"]
