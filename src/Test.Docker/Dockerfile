FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /app
COPY . ./
FROM mcr.microsoft.com/dotnet/runtime:7.0
WORKDIR /app
EXPOSE 8080/tcp
COPY --from=build /app/ .
ENTRYPOINT ["dotnet", "Test.Docker.dll"]
