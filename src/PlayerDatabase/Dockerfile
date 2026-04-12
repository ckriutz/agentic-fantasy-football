# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project file and restore as distinct layer
COPY PlayerDatabase.csproj ./
RUN dotnet restore PlayerDatabase.csproj

# Copy source and publish
COPY . ./
RUN dotnet publish PlayerDatabase.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PlayerDatabase.dll"]
