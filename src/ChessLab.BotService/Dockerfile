# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG APP_VERSION=dev
WORKDIR /src
COPY src/ChessLab.Core/ChessLab.Core.csproj src/ChessLab.Core/
COPY src/ChessLab.Bots/ChessLab.Bots.csproj src/ChessLab.Bots/
COPY src/ChessLab.BotService/ChessLab.BotService.csproj src/ChessLab.BotService/
RUN dotnet restore src/ChessLab.BotService/ChessLab.BotService.csproj
COPY src/ChessLab.Core src/ChessLab.Core
COPY src/ChessLab.Bots src/ChessLab.Bots
COPY src/ChessLab.BotService src/ChessLab.BotService
RUN dotnet publish src/ChessLab.BotService/ChessLab.BotService.csproj \
    -c Release -o /app/publish --no-restore \
    -p:InformationalVersion=${APP_VERSION} \
    -p:IncludeSourceRevisionInInformationalVersion=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends stockfish \
    && rm -rf /var/lib/apt/lists/*
ENV PATH="${PATH}:/usr/games"
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ChessLab.BotService.dll"]
