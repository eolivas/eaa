# Multi-stage Dockerfile for .NET Orders service
# Build context: solution root (e.g., docker build -f src/Orders.Api/Dockerfile .)

# Stage 1: restore — restore NuGet packages
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY Directory.Build.props ./
COPY Orders.sln ./
COPY src/Orders.Domain/Orders.Domain.csproj src/Orders.Domain/
COPY src/Orders.Application/Orders.Application.csproj src/Orders.Application/
COPY src/Orders.Infrastructure/Orders.Infrastructure.csproj src/Orders.Infrastructure/
COPY src/Orders.Api/Orders.Api.csproj src/Orders.Api/
RUN dotnet restore src/Orders.Api/Orders.Api.csproj

# Stage 2: build — compile the application
FROM restore AS build
COPY src/ src/
RUN dotnet build src/Orders.Api/Orders.Api.csproj -c Release --no-restore

# Stage 3: publish — produce a self-contained publish output
FROM build AS publish
RUN dotnet publish src/Orders.Api/Orders.Api.csproj -c Release --no-build -o /app/publish

# Stage 4: final — minimal runtime image with non-root user
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=publish /app/publish .

USER appuser
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
