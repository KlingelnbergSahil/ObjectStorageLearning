FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY src/ObjectStorage.Core/ObjectStorage.Core.csproj src/ObjectStorage.Core/
COPY src/ObjectStorage.S3/ObjectStorage.S3.csproj src/ObjectStorage.S3/
COPY src/ObjectStorage.AzureBlob/ObjectStorage.AzureBlob.csproj src/ObjectStorage.AzureBlob/
COPY src/ObjectStorage.Backup/ObjectStorage.Backup.csproj src/ObjectStorage.Backup/
COPY src/ObjectStorage.Api/ObjectStorage.Api.csproj src/ObjectStorage.Api/

RUN dotnet restore src/ObjectStorage.Api/ObjectStorage.Api.csproj -m:1

COPY . .

RUN dotnet publish src/ObjectStorage.Api/ObjectStorage.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# The API intentionally shells out to docker for this learning POC so it can
# run mongodump, mongorestore, and PBM commands against lab containers.
RUN apt-get update \
    && apt-get install -y --no-install-recommends docker.io ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:5213

EXPOSE 5213

ENTRYPOINT ["dotnet", "ObjectStorage.Api.dll"]
