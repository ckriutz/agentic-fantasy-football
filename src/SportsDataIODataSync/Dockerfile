# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY SportsDataIODataSync.csproj ./
RUN dotnet restore SportsDataIODataSync.csproj

COPY . ./
RUN dotnet publish SportsDataIODataSync.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SportsDataIODataSync.dll"]
