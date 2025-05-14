# ----------------------------
# 1. Build Stage
# ----------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy everything into the container
COPY . .

# Restore and publish the project
RUN dotnet restore SOH.sln
RUN dotnet publish SOHLogisticsBox/SOHLogisticsBox.csproj -c Release -o /app

# ----------------------------
# 2. Runtime Stage
# ----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY SOHLogisticsBox/start.sh .
RUN chmod +x /app/start.sh

# Copy published DLLs
COPY --from=build /app .

# Copy resources needed at runtime
COPY --from=build /src/SOHLogisticsBox/resources ./resources

COPY --from=build /src/SOHLogisticsBox/config.json /config/config.json

# Set config path
ENV MARS_CONFIG_PATH=/config/config.json

# Run the application
#ENTRYPOINT ["dotnet", "SOHLogisticsBox.dll"]
ENTRYPOINT ["./start.sh"]