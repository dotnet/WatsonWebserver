#
#
# IMPORTANT
# Built using Docker Desktop with Windows containers
# 
# See: https://docs.docker.com/engine/examples/dotnetcore/
#
#

# Use SDK
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

# Set the local working directory and copy in the source
WORKDIR /app
COPY *.csproj .
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet build -c Release
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
EXPOSE 8000/tcp
COPY --from=build /app/out .
 
# Set the entrypoint for the container
ENTRYPOINT ["dotnet", "Test.Docker.dll"]