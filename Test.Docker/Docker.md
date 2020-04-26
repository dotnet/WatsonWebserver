# Running Watson in Docker

Keywords: docker dotnet httplistener http.sys http c#

Getting an ```HttpListener``` application (such as any application using Watson) up and running in Docker can be rather tricky given how 1) Docker acts as a network proxy and 2) HttpListener isn't friendly to ```HOST``` header mismatches.  Thus, it is **critical** that you run your containers using ```--user ContainerAdministrator``` to bypass the ```HttpListener``` restrictions.  There are likely ways around this, but I have been unable to find one.  

## Steps to Run Watson Application in Docker

1) View and modify the ```Dockerfile``` as appropriate for your application.

2) Execute the Docker build process:
```
$ docker build -t watsontest -f Dockerfile .
```

3) Verify the image exists:
```
$ docker images
REPOSITORY                              TAG                 IMAGE ID            CREATED             SIZE
watsontest                              latest              047e29f37f9c        2 seconds ago       328MB
mcr.microsoft.com/dotnet/core/sdk       3.1                 abbb476b7b81        11 days ago         737MB
mcr.microsoft.com/dotnet/core/runtime   3.1                 4b555235dfc0        11 days ago         327MB
```
 
4) Execute the container:
```
Windows:
$ docker run --user ContainerAdministrator -d -p 8000:8000 watsontest 

Linux or Mac:
$ docker run --user root -d -p 8000:8000 watsontest
```

5) Connect to Watson in your browser: 
```
http://localhost:8000
```

6) Get the container name:
```
$ docker ps
CONTAINER ID        IMAGE               COMMAND                  CREATED              STATUS              PORTS                    NAMES
3627b4e812fd        watsontest          "dotnet Test.Docker.…"   About a minute ago   Up About a minute   0.0.0.0:8000->8000/tcp   silly_khayyam
```

7) Kill a running container:
```
$ docker kill [CONTAINER ID]
```

8) Delete a container image:
```
$ docker rmi [IMAGE ID] -f
```

## Helpful Notes

While attempting to get Watson up and running in Docker on Windows, I stumbled upon this **really cool** project called DockerProxy.  Check it out, may be helpful for you: https://github.com/Kymeric/DockerProxy.

Here is the ```Dockerfile``` used in the ```Test.Docker``` project:
```
# Use SDK
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build

# Set the local working directory, copy in the project file, and perform a restore
WORKDIR /app
COPY *.csproj .
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet build   -f netcoreapp3.1 -c Release
RUN dotnet publish -f netcoreapp3.1 -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1
WORKDIR /app
EXPOSE 8000/tcp
COPY --from=build /app/out .
 
# Set the entrypoint for the container
ENTRYPOINT ["dotnet", "Test.Docker.dll"]
```

