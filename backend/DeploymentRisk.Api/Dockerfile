FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DeploymentRisk.Api.csproj", "."]
RUN dotnet restore "DeploymentRisk.Api.csproj"
COPY . .
RUN dotnet build "DeploymentRisk.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DeploymentRisk.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DeploymentRisk.Api.dll"]
