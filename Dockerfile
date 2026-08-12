# syntax=docker/dockerfile:1

# ---- Stage 1: Tailwind CSS build (kept separate so the .NET SDK image never needs Node) ----
FROM node:22-alpine AS css
WORKDIR /src
COPY src/BrainAndHand.Web/package.json src/BrainAndHand.Web/package-lock.json src/BrainAndHand.Web/
RUN npm --prefix src/BrainAndHand.Web ci
COPY src/BrainAndHand.Web/Styles src/BrainAndHand.Web/Styles
COPY src/BrainAndHand.Web/Components src/BrainAndHand.Web/Components
COPY src/BrainAndHand.Web.Client src/BrainAndHand.Web.Client
RUN npm --prefix src/BrainAndHand.Web run build:css

# ---- Stage 2: .NET build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BrainAndHand.slnx .
COPY src/ src/
COPY tests/ tests/
COPY --from=css /src/src/BrainAndHand.Web/wwwroot/css/app.css src/BrainAndHand.Web/wwwroot/css/app.css
RUN dotnet restore
RUN dotnet publish src/BrainAndHand.Web/BrainAndHand.Web.csproj \
    -c Release -o /app/publish --no-restore \
    -p:SkipTailwindBuild=true

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends stockfish \
    && rm -rf /var/lib/apt/lists/*
# Debian installs games (incl. stockfish) under /usr/games, which isn't on PATH by default.
ENV PATH="${PATH}:/usr/games"
WORKDIR /app
COPY --from=build /app/publish .
VOLUME /data
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "BrainAndHand.Web.dll"]
