# syntax=docker/dockerfile:1

# 1) Build/publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY NhaTro/Web/Web.csproj NhaTro/Web/
RUN dotnet restore NhaTro/Web/Web.csproj
COPY . .
RUN dotnet publish NhaTro/Web/Web.csproj -c Release -o /app/publish --no-restore

# 2) Migration runner (SDK + dotnet-ef pinned in .config/dotnet-tools.json)
FROM build AS migrate
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "ef", "database", "update", \
    "--project", "NhaTro/Web/Web.csproj", \
    "--startup-project", "NhaTro/Web/Web.csproj", \
    "--configuration", "Release"]

# 3) Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/storage /app/keys /app/logs
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Web.dll"]
