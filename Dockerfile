# syntax=docker/dockerfile:1

# 1) Build the React SPA (needs Node 20.19+/22.12+)
FROM node:22-alpine AS spa
WORKDIR /spa
COPY src/FakeVeresiye.Web/package.json src/FakeVeresiye.Web/package-lock.json ./
RUN npm ci
COPY src/FakeVeresiye.Web/ ./
RUN npm run build

# 2) Publish the API. The SPA is copied in from stage 1, so the csproj's npm build is skipped.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/FakeVeresiye.Api/FakeVeresiye.Api.csproj ./src/FakeVeresiye.Api/
RUN dotnet restore ./src/FakeVeresiye.Api/FakeVeresiye.Api.csproj
COPY src/FakeVeresiye.Api/ ./src/FakeVeresiye.Api/
COPY --from=spa /spa/dist/ ./src/FakeVeresiye.Api/wwwroot/
RUN dotnet publish ./src/FakeVeresiye.Api/FakeVeresiye.Api.csproj \
      -c Release -o /app --no-restore -p:SkipSpaBuild=true

# 3) Runtime — non-root. DB on one mounted volume, the action log on a second, separate one —
# deliberately not under /data, so a problem with the data volume (corruption, a bad restore,
# an accidental wipe) can't take the log that would help diagnose it down with it.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app ./
RUN mkdir -p /data /var/log/fakeveresiye && chown app:app /data /var/log/fakeveresiye
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Default="Data Source=/data/fakeveresiye.db" \
    ActionLog__Path="/var/log/fakeveresiye/actions-.log"
EXPOSE 8080
VOLUME ["/data", "/var/log/fakeveresiye"]
ENTRYPOINT ["dotnet", "FakeVeresiye.Api.dll"]
