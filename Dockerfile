# ==========================================
# Build Stage (.NET 10 SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for optimal layer caching
COPY ["BrandGuardAI.sln", "./"]
COPY ["BrandGuardAI.Domain/BrandGuardAI.Domain.csproj", "BrandGuardAI.Domain/"]
COPY ["BrandGuardAI.Application/BrandGuardAI.Application.csproj", "BrandGuardAI.Application/"]
COPY ["BrandGuardAI.Infrastructure/BrandGuardAI.Infrastructure.csproj", "BrandGuardAI.Infrastructure/"]
COPY ["BrandGuardAI.API/BrandGuardAI.API.csproj", "BrandGuardAI.API/"]
COPY ["BrandGuardAI.Tests/BrandGuardAI.Tests.csproj", "BrandGuardAI.Tests/"]

# Restore dependencies
RUN dotnet restore "BrandGuardAI.API/BrandGuardAI.API.csproj"

# Copy all source code
COPY . .

# Publish BrandGuardAI.API in Release mode
WORKDIR /src/BrandGuardAI.API
RUN dotnet publish "BrandGuardAI.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Runtime Stage (.NET 10 ASP.NET Core Runtime)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Ensure directories for SQLite database and uploaded guideline PDFs exist with full write permissions
RUN mkdir -p /app/uploads/guidelines /app/data /tmp && chmod -R 777 /app/uploads /app/data /tmp

# Configure Production Environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV PORT=10000
ENV DOTNET_ROLL_FORWARD=Major

# Expose Render standard container port
EXPOSE 10000

# Copy published application from build stage
COPY --from=build /app/publish .

# Start the BrandGuard AI API
ENTRYPOINT ["dotnet", "BrandGuardAI.API.dll"]
