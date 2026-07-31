# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY FantasyProsDataSync.csproj ./
RUN dotnet restore FantasyProsDataSync.csproj

COPY . ./
RUN dotnet publish FantasyProsDataSync.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8004
EXPOSE 8004
ENTRYPOINT ["dotnet", "FantasyProsDataSync.dll"]
