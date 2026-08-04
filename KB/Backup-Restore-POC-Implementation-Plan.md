# Backup and Restore POC Implementation Plan

This document maps `gemini.md` and `KB/Download-and-Upload-flow.md` into the current end-to-end learning POC.

The repository now implements the object-storage foundation plus a MongoDB backup/restore learning flow around `mongodump`, `mongorestore`, Percona Server for MongoDB, Percona Backup for MongoDB, and the existing storage abstraction.

## Current implemented foundation

From `gemini.md`, the following parts are already implemented or partially implemented:

| Gemini concept | Current repo status | Existing location |
|---|---|---|
| Unified storage abstraction | Implemented | `src/ObjectStorage.Core` |
| SeaweedFS through S3 API | Implemented | `docker/seaweedfs`, `src/ObjectStorage.S3` |
| S3 presigned GET URL | Implemented | `S3ObjectStorageProvider.CreateDownloadUrlAsync` |
| S3 presigned PUT URL | Implemented | `S3ObjectStorageProvider.CreateUploadUrlAsync` |
| API endpoints for upload/download URL | Implemented | `StorageController` |
| Browser-to-storage direct transfer foundation | Backend implemented | `POST /api/storage/upload-url` |
| Storage-to-browser direct transfer foundation | Backend implemented | `POST /api/storage/download-url` |
| Mongo container discovery | Implemented | `GET /api/backup/mongo-containers` |
| Mongo DB size detection | Implemented | `GET /api/backup/mongo-containers/{container}/databases/{database}/size` |
| Size-based strategy selection | Implemented | `BackupStrategySelector` |
| `mongodump` backup to object storage | Implemented | `POST /api/backup/mongodump` |
| `mongorestore` from object storage | Implemented | `POST /api/backup/mongorestore` |
| Backup catalog/listing | Implemented | `GET /api/backup/records` |
| PBM logical command path | Implemented | `POST /api/backup/pbm/backup` |
| PBM physical command path | Implemented for lab | `POST /api/backup/pbm/backup` |
| Disposable Percona Mongo lab | Implemented | `docker/backup-poc` |
| Simple learning UI | Implemented | `src/ObjectStorage.Api/wwwroot/index.html` |
| Azure Blob abstraction | Skeleton only | `src/ObjectStorage.AzureBlob` |

The full compose POC uses `backup-poc-mongo` as the only Mongo target by default. Imported data can be restored into that disposable database and then used to exercise the size-based strategy paths.

## Target learning POC

The end-to-end POC should prove these workflows:

1. Detect target MongoDB database size.
2. Choose backup strategy based on size.
3. Execute the selected backup mechanism.
4. Store backup output in SeaweedFS through S3.
5. List backup records.
6. Generate a direct download URL for a backup.
7. Generate a direct upload URL for an externally supplied backup archive.
8. Restore from an existing backup.
9. Keep API memory usage out of large file byte transfer paths.

## Implemented shape

Keep object storage and backup orchestration separate.

Backup orchestration lives in a separate class library:

```text
src/
  ObjectStorage.Backup/
    Abstractions/
      IBackupCatalog.cs
      IBackupCommandRunner.cs
      IBackupOrchestrator.cs
      IDatabaseSizeReader.cs
    Configuration/
      BackupOptions.cs
      MongoBackupOptions.cs
      PbmOptions.cs
    Models/
      BackupRecord.cs
      BackupRequest.cs
      BackupRestoreRequest.cs
      BackupStatus.cs
      BackupStrategy.cs
      DatabaseSizeInfo.cs
    Services/
      BackupCatalog.cs
      BackupOrchestrator.cs
      BackupStrategySelector.cs
      DockerCommandRunner.cs
      MongoDatabaseSizeReader.cs
      MongodumpBackupRunner.cs
      MongorestoreRunner.cs
      PbmBackupRunner.cs
      PbmRestoreRunner.cs
    DependencyInjection/
      BackupServiceCollectionExtensions.cs
```

The API exposes backup endpoints:

```text
src/ObjectStorage.Api/
  Controllers/
    BackupController.cs
  Contracts/
    CreateBackupRequest.cs
    CreateRestoreRequest.cs
    BackupRecordResponse.cs
    CreateBackupUploadUrlRequest.cs
    CreateBackupDownloadUrlRequest.cs
```

Docker lab infrastructure is implemented:

```text
docker/
  backup-poc/
    compose.yml
    scripts/
      init-replica-set.js
      init-replica-set.js
      seed-learning-db.js
```

## Project references

Recommended dependencies:

```text
ObjectStorage.Api
  -> ObjectStorage.Core
  -> ObjectStorage.S3
  -> ObjectStorage.AzureBlob
  -> ObjectStorage.Backup

ObjectStorage.Backup
  -> ObjectStorage.Core

ObjectStorage.S3
  -> ObjectStorage.Core

ObjectStorage.AzureBlob
  -> ObjectStorage.Core
```

`ObjectStorage.Backup` should depend on the storage abstraction, not on `ObjectStorage.S3` directly.

## Current configuration

Current shape in `appsettings.Development.json`:

```json
{
  "Backup": {
    "DefaultBackupContainer": "ge-backups",
    "SmallDatabaseThresholdGb": 20,
    "MediumDatabaseThresholdGb": 100,
    "CommandTimeoutMinutes": 120,
    "Docker": {
      "SourceMongoContainerName": "backup-poc-mongo",
      "LabMongoContainerName": "backup-poc-mongo",
      "PbmAgentContainerName": "backup-poc-pbm-agent",
      "LabMongoUriInsideContainer": "mongodb://localhost:27017/?replicaSet=rs0",
      "SourceMongoUriInsideContainer": "mongodb://localhost:27017/?replicaSet=rs0",
      "PbmStorageConfigPath": "/etc/pbm/pbm-storage.yaml"
    }
  }
}
```

For learning, make thresholds configurable so you can force each path without actually creating 20 GB or 100 GB databases.

Example:

```json
{
  "Backup": {
    "SmallDatabaseThresholdGb": 0.01,
    "MediumDatabaseThresholdGb": 0.02
  }
}
```

## Backup strategy selector

Implement:

```text
BackupStrategySelector
```

Rules from `gemini.md`:

| Database size | Strategy | Tool |
|---:|---|---|
| `< 20 GB` | Small logical dump | `mongodump` |
| `20 GB - 100 GB` | Medium logical backup | PBM logical |
| `> 100 GB` | Large physical backup | PBM physical |

POC enum:

```csharp
public enum BackupStrategy
{
    Mongodump,
    PbmLogical,
    PbmPhysical
}
```

The `mongodump` endpoint is intentionally limited to the `Mongodump` strategy. PBM logical and physical backup are exposed through the dedicated `/api/backup/pbm/backup` endpoint.

## MongoDB size detector

Implement:

```text
MongoDatabaseSizeReader
```

Responsibility:

1. Connect to MongoDB.
2. Run `db.stats()` for the requested database.
3. Read `storageSize`, `dataSize`, and `indexSize`.
4. Return `DatabaseSizeInfo`.

Implemented endpoint:

```text
GET /api/backup/mongo-containers/{containerName}/databases/{databaseName}/size
```

Example response:

```json
{
  "databaseName": "production_db",
  "dataSizeBytes": 104857600,
  "storageSizeBytes": 157286400,
  "indexSizeBytes": 5242880,
  "selectedStrategy": "Mongodump"
}
```

## Docker backup lab

Add `docker/backup-poc/compose.yml`.

Services needed:

| Service | Purpose |
|---|---|
| `backup-poc-mongo` or `backup-poc-psmdb` | MongoDB/Percona MongoDB running as replica set `rs0`. |
| `backup-poc-pbm-agent` | PBM sidecar connected to the MongoDB replica set. |
| `backup-poc-tools` | Utility container with `mongodump`, `mongorestore`, AWS CLI or compatible S3 tool. |
| SeaweedFS | Reuse existing `docker/seaweedfs` or include it in a combined compose file. |

Important MongoDB requirement:

```text
--replSet rs0
```

PBM depends on replica set behavior and the oplog.

For physical PBM backup, prefer Percona Server for MongoDB because PBM physical backup support is tied to Percona's server/storage engine support.

## PBM configuration

PBM must point to SeaweedFS S3.

Conceptual config:

```yaml
storage:
  type: s3
  s3:
    region: us-east-1
    bucket: ge-backups
    prefix: pbm
    endpointUrl: http://seaweed-s3:8333
    credentials:
      access-key-id: gearengine-access-key
      secret-access-key: gearengine-secret-key-change-me
```

Add script:

```text
docker/backup-poc/scripts/configure-pbm.sh
```

The script should run:

```bash
pbm config --file /path/to/pbm-storage.yaml
pbm config
pbm status
```

## Tier 1: mongodump backup implementation

Use this for small databases.

Best POC shape:

1. API asks `BackupOrchestrator` to back up database `X`.
2. `BackupStrategySelector` selects `Mongodump`.
3. `MongodumpBackupRunner` executes a command inside `backup-poc-tools`.
4. The command streams directly to S3 if possible.

Preferred no-API-buffer command shape:

```bash
mongodump \
  --uri="mongodb://backup-poc-mongo:27017/<db>?replicaSet=rs0" \
  --archive \
  --gzip \
| aws --endpoint-url http://seaweed-s3:8333 \
    s3 cp - s3://ge-backups/mongodump/<db>/<timestamp>.archive.gz
```

Restore shape:

```bash
aws --endpoint-url http://seaweed-s3:8333 \
  s3 cp s3://ge-backups/mongodump/<db>/<timestamp>.archive.gz - \
| mongorestore \
    --uri="mongodb://backup-poc-mongo:27017/?replicaSet=rs0" \
    --archive \
    --gzip \
    --drop
```

This keeps the C# API out of the file byte path.

Fallback POC shape:

1. `mongodump` to a temp file.
2. Upload temp file with `IObjectStorageProvider.UploadAsync`.
3. Delete temp file.

That is simpler, but it uses API/container disk and is less aligned with `gemini.md`.

## Tier 2: PBM logical backup implementation

Use this for medium databases.

Command:

```bash
docker exec backup-poc-pbm-agent pbm backup --type=logical
```

Then:

```bash
docker exec backup-poc-pbm-agent pbm list
docker exec backup-poc-pbm-agent pbm status
```

Implementation location:

```text
src/ObjectStorage.Backup/Services/PbmBackupRunner.cs
```

Responsibilities:

1. Execute PBM backup.
2. Capture command output.
3. Parse backup name from output or retrieve latest from `pbm list`.
4. Create/update a `BackupRecord`.

## Tier 3: PBM physical backup implementation

Use this for large databases and production-like restore learning.

Command:

```bash
docker exec backup-poc-pbm-agent pbm backup --type=physical
```

Later incremental path:

```bash
docker exec backup-poc-pbm-agent pbm backup --type=incremental
```

Keep incremental backup out of the first backup/restore POC unless physical backup is already working.

## Backup catalog

For learning, start with a JSON file catalog or in-memory catalog.

Recommended first POC:

```text
src/ObjectStorage.Backup/Services/BackupCatalog.cs
```

Store records in:

```text
data/backups/catalog.json
```

Record shape:

```json
{
  "id": "2026-08-03T120000Z-production-db",
  "databaseName": "production_db",
  "strategy": "PbmPhysical",
  "storageContainer": "ge-backups",
  "storageKey": "pbm/2026-08-03T120000Z",
  "status": "Completed",
  "createdAt": "2026-08-03T12:00:00Z",
  "completedAt": "2026-08-03T12:10:00Z",
  "sizeBytes": 123456789
}
```

Later, replace this with database persistence.

## Backup API endpoints

Add:

```text
POST /api/backup
GET  /api/backup
GET  /api/backup/{id}
GET  /api/backup/databases/{databaseName}/size
POST /api/backup/download-url
POST /api/backup/upload-url
POST /api/backup/import-complete
POST /api/backup/restore
```

### `POST /api/backup`

Request:

```json
{
  "databaseName": "production_db",
  "strategyOverride": null
}
```

Behavior:

1. Detect DB size.
2. Select strategy.
3. Run backup command.
4. Return backup record.

### `POST /api/backup/download-url`

Use existing storage provider:

```csharp
IObjectStorageProvider.CreateDownloadUrlAsync(...)
```

This maps directly to `Workflow B` in `gemini.md`.

### `POST /api/backup/upload-url`

Use existing storage provider:

```csharp
IObjectStorageProvider.CreateUploadUrlAsync(...)
```

This maps to external backup upload in `Workflow C`.

### `POST /api/backup/import-complete`

After direct browser upload completes:

1. Verify object metadata exists.
2. Register the uploaded object in the backup catalog.
3. For PBM-format backups, optionally run `pbm resync`.

### `POST /api/backup/restore`

Request:

```json
{
  "backupId": "2026-08-03T120000Z-production-db",
  "targetDatabaseName": "production_db",
  "dropExisting": true
}
```

Behavior:

| Backup strategy | Restore command |
|---|---|
| `Mongodump` | `mongorestore --archive --gzip --drop` |
| `PbmLogical` | `pbm restore <backup-name>` |
| `PbmPhysical` | `pbm restore <backup-name>` |

## Command execution

Implement:

```text
DockerCommandRunner
```

Purpose:

1. Execute controlled `docker exec` commands.
2. Capture stdout/stderr.
3. Capture exit code.
4. Apply timeout.
5. Return structured result.

Do not accept raw command text directly from API requests.

Use typed command builders:

```text
MongodumpBackupRunner
PbmBackupRunner
PbmRestoreRunner
MongorestoreRunner
```

This keeps command injection risk low even in a learning POC.

## Hangfire

`gemini.md` includes Hangfire for scheduled backups.

Recommendation:

1. Do not implement Hangfire first.
2. First prove manual `POST /api/backup`.
3. Then add Hangfire once backup and restore commands are reliable.

Later location:

```text
src/ObjectStorage.Backup/Jobs/ScheduledBackupWorker.cs
```

Endpoint for learning:

```text
POST /api/backup/schedules
```

## Frontend/browser multipart upload

Current API can generate presigned PUT URLs for single-object upload.

Do not add browser multipart first.

Recommended order:

1. Prove direct single PUT upload with curl.
2. Add simple HTML/JS page that requests upload URL and PUTs a file.
3. Add S3 multipart only after backup/restore is working.

Multipart will require new provider-neutral methods or S3-specific learning endpoints:

```text
CreateMultipartUpload
CreatePartUploadUrl
CompleteMultipartUpload
AbortMultipartUpload
```

That is intentionally outside the current storage abstraction.

## End-to-end implementation phases

### Phase 1: current storage POC

Status: mostly complete.

Remaining:

1. Run the manual tests in `KB/Current-S3-POC-Test-Guide.md`.
2. Document SeaweedFS behavior for signed header mismatch and expired URLs.

### Phase 2: Docker MongoDB replica set lab

Implement:

```text
docker/backup-poc/compose.yml
docker/backup-poc/scripts/init-replica-set.js
docker/backup-poc/scripts/seed-small-db.js
```

Goal:

1. MongoDB starts as replica set `rs0`.
2. API can connect.
3. Size detector reads database stats.

### Phase 3: backup module skeleton

Implement:

```text
src/ObjectStorage.Backup
BackupOptions
BackupStrategySelector
MongoDatabaseSizeReader
BackupCatalog
BackupController
```

Goal:

1. `GET /api/backup/databases/{db}/size` works.
2. `POST /api/backup` selects a strategy but can initially return a dry-run response.

### Phase 4: mongodump path

Implement:

```text
MongodumpBackupRunner
MongorestoreRunner
```

Goal:

1. Backup small database.
2. Store archive in SeaweedFS.
3. Download backup by presigned URL.
4. Restore small database from archive.

### Phase 5: PBM logical path

Implement:

```text
PbmBackupRunner
PbmRestoreRunner
docker/backup-poc/scripts/configure-pbm.sh
```

Goal:

1. PBM connects to MongoDB replica set.
2. PBM stores logical backups in SeaweedFS.
3. API can trigger PBM backup and restore.

### Phase 6: PBM physical path

Switch MongoDB lab to Percona Server for MongoDB if not already using it.

Goal:

1. PBM physical backup works.
2. PBM physical restore works.
3. Document limitations and required MongoDB/PSMDB versions.

### Phase 7: scheduling and UI

Add:

```text
Hangfire scheduled worker
simple UI or Swagger-only workflow documentation
backup history persistence
```

Goal:

1. Scheduled backup runs.
2. Manual restore still works.
3. User can download/upload backups without API byte proxying.

## Suggested first coding task after storage tests

Implement Phase 2 and Phase 3 only:

1. Add `ObjectStorage.Backup` project.
2. Add backup options and strategy selector.
3. Add MongoDB replica set Docker lab.
4. Add database size endpoint.
5. Add dry-run backup endpoint that reports selected strategy.

Do not start with PBM restore. Restore is destructive and should come after size detection, strategy selection, storage tests, and backup creation are proven.
