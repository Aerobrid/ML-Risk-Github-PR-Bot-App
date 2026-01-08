# Stage 1: base runtime image (lightweight ASP.NET runtime only, no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

# Stage 2: build environment (SDK with build tools)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# copy project file for restore
COPY ["DeploymentRisk.Api.csproj", "."]
# restore nuget dependencies
RUN dotnet restore "DeploymentRisk.Api.csproj"
# copy full source code
COPY . .
# compile and build release binary
RUN dotnet build "DeploymentRisk.Api.csproj" -c Release -o /app/build

# Stage 3: publish (prepare final artifacts)
FROM build AS publish
# publish release build (no self-contained, will use base runtime)
RUN dotnet publish "DeploymentRisk.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: final runtime image (copy only published files into clean base)
FROM base AS final
WORKDIR /app
# copy published binaries from publish stage
COPY --from=publish /app/publish .
# run the api
ENTRYPOINT ["dotnet", "DeploymentRisk.Api.dll"]
