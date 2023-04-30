#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["GitHub.App.Workflows.WebApi/GitHub.App.Workflows.WebApi.csproj", "GitHub.App.Workflows.WebApi/"]
RUN dotnet restore "GitHub.App.Workflows.WebApi/GitHub.App.Workflows.WebApi.csproj"
COPY . .
WORKDIR "/src/GitHub.App.Workflows.WebApi"

# Generate .NET self-signed certificate
RUN dotnet dev-certs https --clean && \
dotnet dev-certs https -ep ~/.aspnet/https/GitHub.App.Workflows.WebApi.pfx -p 88809a6d-5c36-42bd-9aeb-862f2e02d235 && \
dotnet dev-certs https && \
dotnet dev-certs https --trust

# Run .NET build
RUN dotnet build "GitHub.App.Workflows.WebApi.csproj" -c Release -o /app/build

# Run .NET publish
FROM build AS publish
RUN dotnet publish "GitHub.App.Workflows.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=publish /root/.aspnet /root/.aspnet

ENTRYPOINT ["dotnet", "GitHub.App.Workflows.WebApi.dll"]