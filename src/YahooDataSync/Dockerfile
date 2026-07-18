# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY YahooDataSync.csproj ./
RUN dotnet restore YahooDataSync.csproj

COPY . ./
RUN dotnet publish YahooDataSync.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8003
EXPOSE 8003
ENTRYPOINT ["dotnet", "YahooDataSync.dll"]
