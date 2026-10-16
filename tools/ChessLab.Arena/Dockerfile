# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG APP_VERSION=dev
WORKDIR /src
COPY src/ChessLab.Core/ChessLab.Core.csproj src/ChessLab.Core/
COPY src/ChessLab.Bots/ChessLab.Bots.csproj src/ChessLab.Bots/
COPY src/ChessLab.Data/ChessLab.Data.csproj src/ChessLab.Data/
COPY tools/ChessLab.Arena/ChessLab.Arena.csproj tools/ChessLab.Arena/
RUN dotnet restore tools/ChessLab.Arena/ChessLab.Arena.csproj
COPY src/ChessLab.Core src/ChessLab.Core
COPY src/ChessLab.Bots src/ChessLab.Bots
COPY src/ChessLab.Data src/ChessLab.Data
COPY tools/ChessLab.Arena tools/ChessLab.Arena
RUN dotnet publish tools/ChessLab.Arena/ChessLab.Arena.csproj \
    -c Release -o /app/publish --no-restore \
    -p:InformationalVersion=${APP_VERSION} \
    -p:IncludeSourceRevisionInInformationalVersion=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
ARG APP_VERSION=dev
RUN apt-get update \
    && apt-get install -y --no-install-recommends stockfish \
    && rm -rf /var/lib/apt/lists/*
ENV PATH="${PATH}:/usr/games"
ENV ARENA_COMMIT=${APP_VERSION}
WORKDIR /app
COPY --from=build /app/publish .
COPY tests/engine-data tests/engine-data
ENTRYPOINT ["nice", "-n", "19", "dotnet", "ChessLab.Arena.dll"]
