# syntax=docker/dockerfile:1

# ---- Stage 1: Tailwind CSS build (kept separate so the .NET SDK image never needs Node) ----
FROM node:22-alpine AS css
WORKDIR /src
COPY src/ChessLab.Web/package.json src/ChessLab.Web/package-lock.json src/ChessLab.Web/
RUN npm --prefix src/ChessLab.Web ci
COPY src/ChessLab.Web/Styles src/ChessLab.Web/Styles
COPY src/ChessLab.Web/Components src/ChessLab.Web/Components
COPY src/ChessLab.Web.Client src/ChessLab.Web.Client
RUN npm --prefix src/ChessLab.Web run build:css

# ---- Stage 2: .NET build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG APP_VERSION=dev
WORKDIR /src
COPY ChessLab.slnx .
COPY src/ src/
COPY tests/ tests/
COPY --from=css /src/src/ChessLab.Web/wwwroot/css/app.css src/ChessLab.Web/wwwroot/css/app.css
COPY --from=css /src/src/ChessLab.Web/wwwroot/css/files src/ChessLab.Web/wwwroot/css/files
COPY --from=css /src/src/ChessLab.Web/wwwroot/js/cm-chessboard src/ChessLab.Web/wwwroot/js/cm-chessboard
RUN dotnet restore
RUN dotnet publish src/ChessLab.Web/ChessLab.Web.csproj \
    -c Release -o /app/publish --no-restore \
    -p:SkipTailwindBuild=true -p:InformationalVersion=${APP_VERSION} \
    -p:IncludeSourceRevisionInInformationalVersion=false

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends stockfish \
    && rm -rf /var/lib/apt/lists/*
# Debian installs games (incl. stockfish) under /usr/games, which isn't on PATH by default.
ENV PATH="${PATH}:/usr/games"
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "ChessLab.Web.dll"]
