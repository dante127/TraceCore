# Multi-stage Dockerfile for TraceCore API (.NET 10)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for caching layer
COPY ["src/TraceCore.Domain/TraceCore.Domain.csproj", "src/TraceCore.Domain/"]
COPY ["src/TraceCore.Application/TraceCore.Application.csproj", "src/TraceCore.Application/"]
COPY ["src/TraceCore.Infrastructure/TraceCore.Infrastructure.csproj", "src/TraceCore.Infrastructure/"]
COPY ["src/TraceCore.Api/TraceCore.Api.csproj", "src/TraceCore.Api/"]

# Restore dependencies
RUN dotnet restore "src/TraceCore.Api/TraceCore.Api.csproj"

# Copy all source files and publish
COPY src/ src/
WORKDIR "/src/src/TraceCore.Api"
RUN dotnet publish "TraceCore.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TraceCore.Api.dll"]
