#
#
# IMPORTANT
# Built using Docker Desktop with Windows containers
#
#

# Use SDK
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

# Set the local working directory and copy in the source
WORKDIR /app
COPY . /app
RUN dotnet restore "./Test.Docker.csproj"

# Expose TCP port 8000
EXPOSE 8000/tcp

# Build and publish
RUN dotnet build -c Release "Test.Docker.csproj"
RUN dotnet publish -c Release "Test.Docker.csproj"

# Copy published binary
COPY /app/bin/Release/netcoreapp3.1/publish /app

# Set the entrypoint for the container
ENTRYPOINT ["dotnet", "Test.Docker.dll"]