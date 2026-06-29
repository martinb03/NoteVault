# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as a separate layer for caching
COPY *.csproj ./
RUN dotnet restore NoteVault.csproj

# Copy everything else and build
COPY . .
RUN dotnet publish NoteVault.csproj -c Release -o /app/publish --no-restore


# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
# Installing Chromium runtime dependencies for PuppeteerSharp.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libnss3 \
    libnspr4 \
    libatk1.0-0 \
    libatk-bridge2.0-0 \
    libcups2 \
    libdrm2 \
    libxkbcommon0 \
    libxcomposite1 \
    libxdamage1 \
    libxfixes3 \
    libxrandr2 \
    libgbm1 \
    libpango-1.0-0 \
    libcairo2 \
    libasound2 \
    libatspi2.0-0 \
    libwayland-client0 \
    fonts-liberation \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Copy built application from build stage
COPY --from=build /app/publish .

# Expose application port
EXPOSE 5959

# Configure ASP.NET to listen on all interfaces at 5959
ENV ASPNETCORE_URLS=http://0.0.0.0:5959
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "NoteVault.dll"]