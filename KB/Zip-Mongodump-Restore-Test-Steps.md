# Restore a Zipped Mongodump Backup

This guide explains how to test restore when your backup file is a `.zip` that contains a backup created by `mongodump`.

Important: `mongorestore` does not restore the outer `.zip` file directly. First unzip it, then restore the actual inner `mongodump` output.

## Target rule

Use the disposable lab Mongo container as the restore target:

```text
backup-poc-mongo
```

Do not restore into any source/reference container. Restore only into the disposable POC lab container.

## Start required services

Start SeaweedFS:

```bash
docker compose -f docker/seaweedfs/compose.yml up -d
```

Start the disposable Percona Mongo lab:

```bash
docker compose -f docker/backup-poc/compose.yml up -d
docker exec backup-poc-mongo mongosh --quiet /scripts/init-replica-set.js
```

Start the API:

```bash
dotnet run --project src/ObjectStorage.Api/ObjectStorage.Api.csproj --launch-profile http
```

Use:

```bash
export API=http://localhost:5213
export BUCKET=ge-backups
```

## Step 1: inspect the zip

Put your zip file somewhere local, for example:

```bash
export ZIP_FILE=/tmp/imported-backup.zip
```

Inspect it:

```bash
unzip -l "$ZIP_FILE"
```

You are looking for one of these shapes.

### Shape A: archive gzip file

Example:

```text
imported.archive.gz
```

This usually came from:

```bash
mongodump --db=OriginalDbName --archive --gzip
```

This is the best shape for the current API restore flow.

### Shape B: archive file without gzip

Example:

```text
imported.archive
```

This usually came from:

```bash
mongodump --db=OriginalDbName --archive
```

This can be restored manually, but the current API restore endpoint expects gzip.

### Shape C: dump folder with BSON files

Example:

```text
dump/OriginalDbName/collection1.bson
dump/OriginalDbName/collection1.metadata.json
```

This usually came from:

```bash
mongodump --db=OriginalDbName --out=dump
```

This can be restored manually with `mongorestore --dir`, but the current API restore endpoint does not stream folder dumps from S3.

## Step 2: unzip the backup

```bash
rm -rf /tmp/imported-restore
mkdir -p /tmp/imported-restore
unzip "$ZIP_FILE" -d /tmp/imported-restore
find /tmp/imported-restore -maxdepth 4 -type f | sort
```

## Option 1: restore directly into the lab container

Use this first because it proves the backup file is valid before testing object storage.

### If the zip contains `.archive.gz`

```bash
export ARCHIVE_FILE=/tmp/imported-restore/imported.archive.gz

docker cp "$ARCHIVE_FILE" backup-poc-mongo:/tmp/imported.archive.gz

docker exec backup-poc-mongo mongorestore \
  --uri='mongodb://localhost:27017/?replicaSet=rs0' \
  --archive=/tmp/imported.archive.gz \
  --gzip \
  --drop
```

### If the zip contains `.archive`

```bash
export ARCHIVE_FILE=/tmp/imported-restore/imported.archive

docker cp "$ARCHIVE_FILE" backup-poc-mongo:/tmp/imported.archive

docker exec backup-poc-mongo mongorestore \
  --uri='mongodb://localhost:27017/?replicaSet=rs0' \
  --archive=/tmp/imported.archive \
  --drop
```

### If the zip contains a dump folder

Adjust `DUMP_DIR` so it points to the folder that contains the dumped database folders.

```bash
export DUMP_DIR=/tmp/imported-restore/dump

docker cp "$DUMP_DIR" backup-poc-mongo:/tmp/imported-dump

docker exec backup-poc-mongo mongorestore \
  --uri='mongodb://localhost:27017/?replicaSet=rs0' \
  --dir=/tmp/imported-dump \
  --drop
```

## Step 3: verify restore

Check that the restored database exists in the lab container:

```bash
docker exec backup-poc-mongo mongosh --quiet --eval \
  'JSON.stringify(db.adminCommand({ listDatabases: 1 }).databases)'
```

Check database stats:

```bash
docker exec backup-poc-mongo mongosh --quiet --eval \
  'JSON.stringify(db.getSiblingDB("OriginalDbName").stats())'
```

Check collections:

```bash
docker exec backup-poc-mongo mongosh --quiet --eval \
  'JSON.stringify(db.getSiblingDB("OriginalDbName").getCollectionNames())'
```

## Option 2: upload the inner archive to SeaweedFS and restore through the API

Use this only when the zip contains an inner `.archive.gz` file.

The API restore endpoint currently streams an object storage file into:

```text
mongorestore --archive --gzip
```

So upload the unzipped `.archive.gz`, not the outer `.zip`.

### Create a presigned upload URL

```bash
curl -s -X POST "$API/api/backup/upload-url" \
  -H "Content-Type: application/json" \
  -d '{
    "storageContainer": "ge-backups",
    "storageKey": "manual-upload/imported.archive.gz",
    "contentType": "application/gzip",
    "expiryMinutes": 15
  }'
```

Copy the returned `url` value into:

```bash
export UPLOAD_URL='<paste-url-here>'
```

Upload the inner archive:

```bash
curl -i -X PUT "$UPLOAD_URL" \
  -H "Content-Type: application/gzip" \
  --data-binary @/tmp/imported-restore/imported.archive.gz
```

### Restore through the API

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
  }'
```

Then run the verification commands from Step 3.

## Option 3: test download of the backup object

Create a presigned download URL:

```bash
curl -s -X POST "$API/api/backup/download-url" \
  -H "Content-Type: application/json" \
  -d '{
    "storageContainer": "ge-backups",
    "storageKey": "manual-upload/imported.archive.gz",
    "downloadFileName": "imported.archive.gz",
    "expiryMinutes": 15
  }'
```

Download with the returned URL:

```bash
export DOWNLOAD_URL='<paste-url-here>'
curl -L "$DOWNLOAD_URL" -o /tmp/downloaded-imported.archive.gz
ls -lh /tmp/downloaded-imported.archive.gz
```

## Expected result

After restore, `backup-poc-mongo` should contain the restored database and collections. If you used namespace remapping, verify the target database name such as `poc_large`.

The current POC proves:

1. A zipped external backup can be inspected and unzipped.
2. The actual mongodump artifact can be restored directly into the disposable lab.
3. If the inner file is `.archive.gz`, it can be uploaded to SeaweedFS and restored through the API.
4. The outer `.zip` is only packaging; it is not the file passed to `mongorestore`.

## Current limitation

The API restore endpoint currently supports streamed `.archive.gz` restores only. For `.archive` without gzip or folder-based dump restores, use the manual Docker restore commands above, or add separate API endpoints later for those formats.
