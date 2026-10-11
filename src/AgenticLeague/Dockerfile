# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY AgenticLeague.csproj ./
RUN dotnet restore AgenticLeague.csproj

COPY . ./
RUN dotnet publish AgenticLeague.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0-preview AS final
WORKDIR /app

COPY --from=build /app/publish .

# One-shot job. Override the args to run another mode, for example: --mode draft
ENTRYPOINT ["dotnet", "AgenticLeague.dll"]
CMD ["--mode", "season"]
