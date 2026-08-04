# Full Compose POC Test Steps

Use this flow when you want to run the learning POC without Visual Studio.

The full stack compose file is:

```text
compose.full.yml
```

It starts:

| Service | Purpose |
|---|---|
| `seaweed-s3` | Single SeaweedFS dev container running only master, volume, filer, and S3 gateway |
| `backup-poc-mongo` | Disposable Percona Mongo restore target |
| `backup-poc-pbm-agent` | PBM agent for logical/physical backups |
| `object-storage-api` | .NET REST API + Swagger running with `dotnet watch` hot reload |
| `object-storage-blazor` | MudBlazor Server UI running with `dotnet watch` hot reload |

The stack does not depend on any external Mongo source container. Import backup data into `backup-poc-mongo`, then use that disposable database for the POC.

SeaweedFS is configured in `compose.full.yml` with 1GB volume files and `-volume.max=80`. Keep this capacity high enough for PBM tests because PBM logical/physical backups write multipart objects plus oplog data into the `ge-pbm-backups` bucket.

## Start Everything

If you already started the old split compose files, stop them first to avoid container-name conflicts:

```bash
docker compose -f docker/backup-poc/compose.yml down
docker compose -f docker/seaweedfs/compose.yml down
```

Then start the full stack:

```bash
docker compose -f compose.full.yml up -d --build
```

The API uses:

```text
docker/api/Dockerfile.dev
```

The Blazor UI uses:

```text
docker/blazor/Dockerfile.dev
```

Source code is mounted into both containers and `dotnet watch` restarts the service when C# or Razor files change. The dev Dockerfiles disable polling watcher mode because polling can crash on a Windows bind mount after many generated files exist.

Initialize the lab Mongo replica set:

```bash
docker exec backup-poc-mongo mongosh --quiet /scripts/init-replica-set.js
```

Optional seed data:

```bash
docker exec backup-poc-mongo mongosh --quiet /scripts/seed-learning-db.js
```

## Open UI and Swagger

```text
http://localhost:5214/
http://localhost:5213/swagger
```

## Connect With MongoDB Compass

Use this connection string:

```text
mongodb://localhost:37017/?replicaSet=rs0&directConnection=true
```

After restore, inspect the restored database name, for example `poc_large`, `GNA`, or whichever name you used during namespace remap.

SeaweedFS S3 API:

```text
http://localhost:8333
```

## Verify API Is Running

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5213/swagger/v1/swagger.json
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5214/
```

Expected:

```text
200
200
```

## Verify Docker Access From API

The API container mounts the Docker socket because this learning POC runs `docker ps`, `docker exec mongodump`, `docker exec mongorestore`, and `docker exec pbm`.

```bash
curl -s http://localhost:5213/api/backup/mongo-containers
```

Expected: JSON list containing `backup-poc-mongo`.

## Verify Lab DB Size

```bash
curl -s http://localhost:5213/api/backup/mongo-containers/backup-poc-mongo/databases
curl -s http://localhost:5213/api/backup/mongo-containers/backup-poc-mongo/databases/learning_poc/size
```

## Blazor Timing-Based Test Flow

Use the MudBlazor UI for the main POC test:

```text
http://localhost:5214/
```

Each page has a `Timing Log`. Use `Clear` when you want a fresh timing run.

Timing entries are also persisted by the Blazor container in a Docker volume:

```text
blazor-timing-data -> /data/timings/timing-log.json
```

Inspect the persisted timing file:

```bash
docker exec object-storage-blazor cat /data/timings/timing-log.json
```

The timing file survives container restart. It is removed only when you delete volumes with:

```bash
docker compose -f compose.full.yml down -v
```

Suggested timing table:

| Step | Page | Action | Expected Result | Duration |
|---:|---|---|---|---|
| 1 | Mongodump | Load Databases | `backup-poc-mongo` databases are shown | |
| 2 | Mongodump | Read Size | Selected strategy is shown | |
| 3 | Mongodump Restore tab | Upload And Register | Uploaded file appears in Mongodump Records with Uploaded source | |
| 4 | Mongodump Restore tab | Restore uploaded record with Drop enabled | DB is restored into `backup-poc-mongo` | |
| 5 | Mongodump | Read Size for restored DB | Restored DB size is shown | |
| 6 | Mongodump Backup tab | Backup To SeaweedFS | New backup record appears | |
| 7 | Mongodump table | Download backup record | Browser downloads through presigned URL | |
| 8 | Mongodump Restore tab | Restore backup record with Drop enabled | Backup restores successfully | |
| 9 | PBM Logical Backup tab | Start Logical Backup | UI polls `pbm status`, shows PBM logs, and records full backup duration | |
| 10 | PBM Logical Backup or Restore tab | Download Bundle | Browser downloads a ZIP of the PBM S3 snapshot prefix | |
| 11 | PBM Logical Restore tab | Refresh Snapshots | Completed PBM backup name appears in the table | |
| 12 | PBM Logical Restore tab | Restore Selected Snapshot with Drop enabled | Selected database is dropped, then PBM restore is submitted | |
| 13 | PBM Physical Backup tab | Start Physical Backup | UI polls `pbm status`, shows PBM logs, and records full backup duration | |
| 14 | PBM Physical Backup or Restore tab | Download Bundle | Browser downloads a ZIP of the PBM S3 snapshot prefix | |
| 15 | PBM Physical Restore tab | Refresh Snapshots | Completed physical backup name appears in the table | |
| 16 | PBM Physical Restore tab | Restore Selected Snapshot with Drop enabled | Selected database is dropped, then PBM restore is submitted | |

### Blazor Mongodump Page

Use:

```text
http://localhost:5214/
```

Default values:

| Field | Value |
|---|---|
| Mongo Container | `backup-poc-mongo` |
| Restore Target Container | `backup-poc-mongo` |
| Storage Container | `ge-backups` |

For a plain `.archive` upload:

| Field | Value |
|---|---|
| Storage Key | `manual-upload/imported.archive` |
| File is `.archive.gz` | unchecked |

For a compressed `.archive.gz` upload:

| Field | Value |
|---|---|
| Storage Key | `manual-upload/imported.archive.gz` |
| File is `.archive.gz` | checked |

If you want the restored database renamed, fill:

| Field | Example |
|---|---|
| Source DB Name | `GNA` |
| Target DB Name | `poc_large` |

Keep `Drop existing data before restore` checked when you want a clean restore test.

### Blazor PBM Logical Page

Use:

```text
http://localhost:5214/pbm-logical
```

Run:

1. Open the Backup tab.
2. Click Start Logical Backup.
3. Keep the page open while it polls status and logs.
4. Click Refresh Snapshots if the completed snapshot table has not refreshed yet.
5. Click Download Bundle on the completed snapshot when you want to test PBM backup download throughput.
6. Open the Restore tab.
7. Click Refresh Snapshots.
8. Select one completed snapshot from the table.
9. To test a clean restore, check `Drop database before restore` and keep `Database To Drop` as `R300`, or change it to the database you restored into the POC Mongo container.
10. Click Restore Selected Snapshot.

PBM backup submission can return quickly, but the actual backup continues inside `pbm-agent`. The Blazor page records the full duration only after PBM status/list show that the snapshot finished.

PBM snapshots are stored as multiple S3 objects under the PBM prefix, not as one archive file. The POC download action streams that prefix as a ZIP bundle so download speed can still be tested from the UI. PBM restore itself still uses the snapshot already stored in S3.

### Blazor PBM Physical Page

Use:

```text
http://localhost:5214/pbm-physical
```

Run:

1. Open the Backup tab.
2. Click Start Physical Backup.
3. Keep the page open while it polls status and logs.
4. Click Refresh Snapshots if the completed snapshot table has not refreshed yet.
5. Click Download Bundle on the completed snapshot when you want to test PBM backup download throughput.
6. Open the Restore tab.
7. Click Refresh Snapshots.
8. Select one completed snapshot from the table.
9. To test a clean restore, check `Drop database before restore` and keep `Database To Drop` as `R300`, or change it to the database you restored into the POC Mongo container.
10. Click Restore Selected Snapshot.

PBM physical backup also uses PBM snapshot names, not single mongodump archive files. Restore from a PBM backup by selecting the snapshot name from `pbm list`.

Physical backup is valid in this POC because `backup-poc-mongo` uses Percona Server for MongoDB.

## Restore a Zipped Mongodump Backup

If your backup is a `.zip`, unzip it first. Upload or restore the inner mongodump artifact, not the outer zip.

Use the detailed guide:

```text
KB/Zip-Mongodump-Restore-Test-Steps.md
```

If the original dump database name is different from your desired POC database name, pass namespace remap values:

```bash
curl -s -X POST http://localhost:5213/api/backup/mongorestore \
  -H "Content-Type: application/json" \
  -d '{
    "targetContainerName": "backup-poc-mongo",
    "mongoUri": null,
    "storageContainer": "ge-backups",
    "storageKey": "manual-upload/imported.archive.gz",
    "sourceDatabaseName": "OriginalDbName",
    "targetDatabaseName": "poc_large",
    "dropExisting": true
  }'
```

## PBM Test

Configure PBM:

```bash
curl -s -X POST http://localhost:5213/api/backup/pbm/configure
curl -s http://localhost:5213/api/backup/pbm/status
```

Run logical backup:

```bash
curl -s -X POST http://localhost:5213/api/backup/pbm/backup \
  -H "Content-Type: application/json" \
  -d '{ "strategy": "PbmLogical" }'
```

List PBM backups:

```bash
curl -s http://localhost:5213/api/backup/pbm/list
```

Physical backup is for the disposable Percona lab only:

```bash
curl -s -X POST http://localhost:5213/api/backup/pbm/backup \
  -H "Content-Type: application/json" \
  -d '{ "strategy": "PbmPhysical" }'
```

## Stop Everything

Stop containers but keep volumes:

```bash
docker compose -f compose.full.yml down
```

Delete lab data too:

```bash
docker compose -f compose.full.yml down -v
```
