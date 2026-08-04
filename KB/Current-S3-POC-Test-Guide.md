# Current S3 Object Storage POC

This document explains what is currently implemented in this learning repository and how to test it end to end with SeaweedFS through its S3-compatible API.

## What is implemented

The current implementation proves the object storage foundation needed by the later database backup and restore POC.

Implemented projects:

| Project | Current role |
|---|---|
| `ObjectStorage.Core` | Provider-neutral models and `IObjectStorageProvider` abstraction. |
| `ObjectStorage.S3` | S3 implementation using `AWSSDK.S3`; configured for SeaweedFS. |
| `ObjectStorage.AzureBlob` | Azure Blob options and DI shell only; real Azure provider is not implemented yet. |
| `ObjectStorage.Api` | HTTP API exposing storage operations and temporary access URL generation. |
| `docker/seaweedfs` | Local SeaweedFS master, volume, filer, and S3 gateway. |

Implemented storage operations:

| Capability | Implemented | Where |
|---|---:|---|
| Create bucket/container | Yes | `S3ObjectStorageProvider.EnsureContainerExistsAsync` |
| Server-side upload | Yes | `StorageController.server-upload` |
| Direct client upload URL | Yes | `StorageController.upload-url` |
| Direct client download URL | Yes | `StorageController.download-url` |
| Metadata lookup | Yes | `StorageController.metadata` |
| Existence check | Yes | Used internally before download URL generation |
| Delete object | Yes | `StorageController.DELETE` |
| Mongo container/database discovery | Yes | `BackupController.mongo-containers` |
| DB size detection and strategy selection | Yes | `BackupController.size` |
| `mongodump` backup to object storage | Yes | `BackupController.mongodump` |
| `mongorestore` from object storage | Yes | `BackupController.mongorestore` |
| PBM logical/physical command wrapper | Yes | `BackupController.pbm/*` |
| Simple browser learning UI | Yes | `src/ObjectStorage.Api/wwwroot/index.html` |
| Azure SAS URLs | No | Planned for Azure milestone |
| Multipart browser upload | No | Planned after basic backup/restore POC |
| Hangfire scheduled backups | No | Planned later |

## Current configuration

Development settings are in `src/ObjectStorage.Api/appsettings.Development.json`.

Current provider:

```json
{
  "ObjectStorage": {
    "Provider": "S3",
    "DefaultContainer": "ge-backups",
    "S3": {
      "ServiceUrl": "http://localhost:8333",
      "AccessKey": "gearengine-access-key",
      "SecretKey": "gearengine-secret-key-change-me",
      "Region": "us-east-1",
      "ForcePathStyle": true,
      "UseHttp": true
    }
  }
}
```

SeaweedFS local endpoints:

| Service | URL |
|---|---|
| SeaweedFS master UI | `http://localhost:9333` |
| SeaweedFS filer UI | `http://localhost:8888` |
| SeaweedFS S3 API | `http://localhost:8333` |
| API Swagger UI | `http://localhost:5213/swagger` |
| Backup learning UI | `http://localhost:5213/` |

## Build verification

Use single-threaded restore/build in this workspace. This avoids MSBuild project-reference races on the mounted Windows filesystem.

```bash
dotnet restore src/ObjectStorage.Api/ObjectStorage.Api.csproj --ignore-failed-sources -m:1
dotnet build src/ObjectStorage.Api/ObjectStorage.Api.csproj --no-restore
```

Expected result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Validate the SeaweedFS compose file:

```bash
docker compose -f docker/seaweedfs/compose.yml config --quiet
```

Expected result: no output and exit code `0`.

## Start SeaweedFS

```bash
docker compose -f docker/seaweedfs/compose.yml up -d
docker compose -f docker/seaweedfs/compose.yml ps
docker logs seaweed-s3 --tail 80
```

Expected containers:

```text
seaweed-master
seaweed-volume
seaweed-filer
seaweed-s3
```

The S3 gateway should listen on `http://localhost:8333`.

## Start the API

In a separate terminal:

```bash
dotnet run --project src/ObjectStorage.Api/ObjectStorage.Api.csproj --launch-profile http
```

Expected API URL:

```text
http://localhost:5213
```

Swagger:

```text
http://localhost:5213/swagger
```

Important: the API creates the configured default container (`ge-backups`) during startup. If SeaweedFS is not running, API startup can fail.

## Test data setup

Create a small test file:

```bash
printf 'Object storage learning POC\n' > /tmp/object-storage-learning.txt
export API=http://localhost:5213
export BUCKET=ge-backups
```

## Test 1: create a bucket/container

```bash
curl -i -X POST "$API/api/storage/containers/$BUCKET"
```

Expected result:

```text
HTTP/1.1 204 No Content
```

## Test 2: upload through the API

This is a learning endpoint for small files. Large files should use direct presigned upload URLs.

```bash
curl -i \
  -F "container=$BUCKET" \
  -F "objectKey=learning/server-upload.txt" \
  -F "file=@/tmp/object-storage-learning.txt;type=text/plain" \
  "$API/api/storage/server-upload"
```

Expected result:

```text
HTTP/1.1 201 Created
```

Expected JSON body:

```json
{
  "container": "ge-backups",
  "key": "learning/server-upload.txt",
  "length": 28
}
```

The exact `length` depends on the file content.

## Test 3: read metadata

```bash
curl -s \
  "$API/api/storage/metadata?container=$BUCKET&objectKey=learning/server-upload.txt"
```

Expected JSON shape:

```json
{
  "container": "ge-backups",
  "key": "learning/server-upload.txt",
  "size": 28,
  "contentType": "text/plain",
  "eTag": "...",
  "lastModified": "...",
  "customMetadata": {
    "original-file-name": "object-storage-learning.txt"
  }
}
```

If `jq` is installed, use:

```bash
curl -s "$API/api/storage/metadata?container=$BUCKET&objectKey=learning/server-upload.txt" | jq .
```

## Test 4: generate a direct upload URL

```bash
curl -s -X POST "$API/api/storage/upload-url" \
  -H "Content-Type: application/json" \
  -d '{
    "container": "ge-backups",
    "objectKey": "learning/direct-upload.txt",
    "contentType": "text/plain",
    "expiryMinutes": 15
  }'
```

Expected JSON shape:

```json
{
  "url": "http://localhost:8333/ge-backups/learning/direct-upload.txt?...",
  "httpMethod": "PUT",
  "expiresAt": "...",
  "requiredHeaders": {
    "Content-Type": "text/plain"
  }
}
```

The `Content-Type` header is signed. The client must send the same value during upload.

With `jq`:

```bash
UPLOAD_URL=$(
  curl -s -X POST "$API/api/storage/upload-url" \
    -H "Content-Type: application/json" \
    -d '{
      "container": "ge-backups",
      "objectKey": "learning/direct-upload.txt",
      "contentType": "text/plain",
      "expiryMinutes": 15
    }' | jq -r '.url'
)
```

Without `jq`, copy the `url` value from the response manually.

## Test 5: upload directly to SeaweedFS using the presigned URL

```bash
curl -i -X PUT "$UPLOAD_URL" \
  -H "Content-Type: text/plain" \
  --data-binary @/tmp/object-storage-learning.txt
```

Expected result:

```text
HTTP/1.1 200 OK
```

or another successful 2xx S3 response.

Verify metadata:

```bash
curl -s \
  "$API/api/storage/metadata?container=$BUCKET&objectKey=learning/direct-upload.txt" | jq .
```

## Test 6: generate a direct download URL

```bash
DOWNLOAD_URL=$(
  curl -s -X POST "$API/api/storage/download-url" \
    -H "Content-Type: application/json" \
    -d '{
      "container": "ge-backups",
      "objectKey": "learning/direct-upload.txt",
      "downloadFileName": "downloaded-learning.txt",
      "expiryMinutes": 15
    }' | jq -r '.url'
)
```

Expected response shape:

```json
{
  "url": "http://localhost:8333/ge-backups/learning/direct-upload.txt?...",
  "httpMethod": "GET",
  "expiresAt": "...",
  "requiredHeaders": {}
}
```

## Test 7: download directly from SeaweedFS

```bash
curl -L "$DOWNLOAD_URL" -o /tmp/object-storage-downloaded.txt
cmp /tmp/object-storage-learning.txt /tmp/object-storage-downloaded.txt
```

Expected result: `cmp` prints nothing and exits with code `0`.

That proves the downloaded bytes match the uploaded bytes.

## Test 8: delete the object

```bash
curl -i -X DELETE \
  "$API/api/storage?container=$BUCKET&objectKey=learning/direct-upload.txt"
```

Expected result:

```text
HTTP/1.1 204 No Content
```

Confirm metadata now returns `404`:

```bash
curl -i \
  "$API/api/storage/metadata?container=$BUCKET&objectKey=learning/direct-upload.txt"
```

Expected result:

```text
HTTP/1.1 404 Not Found
```

## Test 9: changed signed header behavior

Generate a URL signed with `text/plain`, then intentionally upload with a different `Content-Type`.

```bash
BAD_UPLOAD_URL=$(
  curl -s -X POST "$API/api/storage/upload-url" \
    -H "Content-Type: application/json" \
    -d '{
      "container": "ge-backups",
      "objectKey": "learning/bad-header.txt",
      "contentType": "text/plain",
      "expiryMinutes": 15
    }' | jq -r '.url'
)

curl -i -X PUT "$BAD_UPLOAD_URL" \
  -H "Content-Type: application/octet-stream" \
  --data-binary @/tmp/object-storage-learning.txt
```

Expected result: S3 should reject the request because the signed header changed.

If SeaweedFS accepts it, document that as a SeaweedFS S3 compatibility difference to investigate before relying on signed upload headers for security decisions.

## Test 10: expired URL behavior

The API currently allows a minimum expiry of 1 minute.

```bash
SHORT_URL=$(
  curl -s -X POST "$API/api/storage/download-url" \
    -H "Content-Type: application/json" \
    -d '{
      "container": "ge-backups",
      "objectKey": "learning/server-upload.txt",
      "downloadFileName": "expired.txt",
      "expiryMinutes": 1
    }' | jq -r '.url'
)

sleep 75
curl -i -L "$SHORT_URL"
```

Expected result: S3 should reject the expired URL.

## Test 11: open the backup learning UI

The API now serves a simple learning UI from:

```text
http://localhost:5213/
```

Use it to inspect Mongo containers, list databases, create a `mongodump` backup into SeaweedFS, restore that object into the lab Mongo container, and run PBM commands.

## Test 12: discover Mongo containers

```bash
curl -s "$API/api/backup/mongo-containers" | jq .
```

Expected result: running Mongo containers are listed. In the full compose POC, `backup-poc-mongo` should be listed.

## Test 13: list lab Mongo databases

```bash
curl -s "$API/api/backup/mongo-containers/backup-poc-mongo/databases" | jq .
```

Expected result: databases include `learning_poc` if you ran the seed script, plus Mongo system databases.

## Test 14: read lab database size and selected strategy

```bash
curl -s "$API/api/backup/mongo-containers/backup-poc-mongo/databases/learning_poc/size" | jq .
```

Expected result shape:

```json
{
  "containerName": "backup-poc-mongo",
  "databaseName": "learning_poc",
  "dataSizeBytes": 12345,
  "storageSizeBytes": 24576,
  "indexSizeBytes": 4096,
  "selectedStrategy": "Mongodump"
}
```

Because this database is below the configured 20 GB threshold, the POC selects the `mongodump` path.

## Test 15: create a lab mongodump backup into SeaweedFS

```bash
curl -s -X POST "$API/api/backup/mongodump" \
  -H "Content-Type: application/json" \
  -d '{
    "sourceContainerName": "backup-poc-mongo",
    "databaseName": "learning_poc",
    "mongoUri": "mongodb://localhost:27017/?replicaSet=rs0",
    "storageContainer": "ge-backups",
    "storageKey": "mongo/learning-poc/learning-poc.archive.gz"
  }' | jq .
```

This streams:

```text
docker exec backup-poc-mongo mongodump --archive --gzip
```

directly into the configured S3-compatible provider. The dump bytes do not get buffered into API memory as one large byte array.

## Test 16: import an external mongodump archive

If you have a zipped backup, unzip it first and upload the inner `.archive.gz` file. Use the detailed guide:

```text
KB/Zip-Mongodump-Restore-Test-Steps.md
```

## Test 17: restore an archive into the lab Mongo container

This example restores a backup object and remaps the original database name to `poc_large`.

```bash
curl -s -X POST "$API/api/backup/mongorestore" \
  -H "Content-Type: application/json" \
  -d '{
    "targetContainerName": "backup-poc-mongo",
    "mongoUri": "mongodb://localhost:27017/?replicaSet=rs0",
    "storageContainer": "ge-backups",
    "storageKey": "manual-upload/imported.archive.gz",
    "sourceDatabaseName": "OriginalDbName",
    "targetDatabaseName": "poc_large",
    "dropExisting": true
  }' | jq .
```

Verify inside the lab container:

```bash
docker exec backup-poc-mongo mongosh --quiet --eval 'db.getSiblingDB("poc_large").stats()'
```

## Test 18: configure and test PBM

PBM uses its own bucket/prefix in SeaweedFS:

```bash
curl -s -X POST "$API/api/backup/pbm/configure" | jq .
curl -s "$API/api/backup/pbm/status" | jq .
curl -s -X POST "$API/api/backup/pbm/backup" \
  -H "Content-Type: application/json" \
  -d '{ "strategy": "PbmLogical" }' | jq .
curl -s "$API/api/backup/pbm/list" | jq .
```

Physical backup can be tested only against the disposable Percona Server for MongoDB lab:

```bash
curl -s -X POST "$API/api/backup/pbm/backup" \
  -H "Content-Type: application/json" \
  -d '{ "strategy": "PbmPhysical" }' | jq .
```

Run PBM physical backup only against the disposable Percona lab.

## What this proves for the backup POC

This proves the most important storage behavior from `gemini.md` and `KB/Download-and-Upload-flow.md`:

1. The C# API can hold permanent storage credentials server-side.
2. The C# API can generate short-lived scoped URLs.
3. Browser/client uploads can go directly to object storage.
4. Browser/client downloads can come directly from object storage.
5. Large backup files do not need to flow through API memory during direct upload/download.
6. Mongo logical backup can stream from `mongodump` to object storage.
7. Mongo logical restore can stream from object storage to `mongorestore`.
8. PBM logical/physical commands can be exercised against a replica set lab.

## What is not implemented yet

Not implemented in the current POC:

| Area | Status |
|---|---|
| Automated backup scheduler | Not implemented |
| Hangfire scheduled backups | Not implemented |
| Browser multipart upload for very large archives | Not implemented |
| Real Azure Blob provider | Not implemented |
| Production auth/authorization around backup APIs | Not implemented |
| Backup retention policy and pruning | Not implemented |
| Automated integration tests for PBM | Not implemented |
