# syntax=docker/dockerfile:1

# --- Build stage: restore + publish with the full SDK ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project file first so `restore` caches unless dependencies change.
COPY Heardit.csproj .
RUN dotnet restore Heardit.csproj

# Copy the rest of the source and publish a framework-dependent app.
COPY . .
RUN dotnet publish Heardit.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- Runtime stage: slim ASP.NET image, non-root ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Kestrel listens on 8080 (plain HTTP); TLS is terminated by the reverse proxy in front.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# The aspnet image ships a non-root `app` user; run as it.
USER app

ENTRYPOINT ["dotnet", "Heardit.dll"]
