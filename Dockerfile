# syntax=docker/dockerfile:1

# ---- Build stage: needs BOTH the .NET SDK and Node, because R3.csproj's
#      BuildSpa target runs `yarn install` + `yarn build` during `dotnet publish`
#      and copies Client/dist into wwwroot. ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Let Corepack download the Yarn version pinned in package.json without prompting
ENV COREPACK_ENABLE_DOWNLOAD_PROMPT=0

# Node 24 + yarn (Corepack ships with Node) for the SPA build
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && curl -fsSL https://deb.nodesource.com/setup_24.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && corepack enable \
    && rm -rf /var/lib/apt/lists/*

# Restore as its own layer so dependency changes don't bust the cache on every edit
COPY R3.csproj ./
RUN dotnet restore R3.csproj

# Bring in the rest and publish (BuildSpa runs yarn here -> wwwroot)
COPY . .
RUN dotnet publish R3.csproj -c Release -o /app/publish

# ---- Runtime stage: ASP.NET runtime only, no SDK/Node ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish ./

# Zeabur routes to the EXPOSEd port; bind Kestrel to it on all interfaces.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "R3.dll"]
