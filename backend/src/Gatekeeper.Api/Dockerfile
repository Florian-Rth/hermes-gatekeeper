# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Gatekeeper.sln ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/Gatekeeper.Api/Gatekeeper.Api.csproj src/Gatekeeper.Api/
COPY src/Gatekeeper.Application/Gatekeeper.Application.csproj src/Gatekeeper.Application/
COPY src/Gatekeeper.Core/Gatekeeper.Core.csproj src/Gatekeeper.Core/
COPY src/Gatekeeper.Infrastructure/Gatekeeper.Infrastructure.csproj src/Gatekeeper.Infrastructure/
COPY tests/Gatekeeper.Tests/Gatekeeper.Tests.csproj tests/Gatekeeper.Tests/

RUN dotnet restore Gatekeeper.sln

COPY . ./
RUN dotnet publish src/Gatekeeper.Api/Gatekeeper.Api.csproj \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "Gatekeeper.Api.dll"]
