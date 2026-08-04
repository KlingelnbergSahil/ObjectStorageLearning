# Backup POC Docker Lab

This lab is disposable. Use it as the restore target for imported backup data.

## Start prerequisites

SeaweedFS must be running first:

```bash
docker compose -f docker/seaweedfs/compose.yml up -d
```

Start the backup lab:

```bash
docker compose -f docker/backup-poc/compose.yml up -d
```

Initialize the replica set:

```bash
docker exec backup-poc-mongo mongosh --quiet /scripts/init-replica-set.js
```

Seed a small learning database:

```bash
docker exec backup-poc-mongo mongosh --quiet /scripts/seed-learning-db.js
```

## Configure PBM

The API can run this through `POST /api/backup/pbm/configure`, or you can run it manually:

```bash
docker exec backup-poc-pbm-agent pbm config --file /etc/pbm/pbm-storage.yaml
docker exec backup-poc-pbm-agent pbm status
```

PBM stores backups in the SeaweedFS S3 bucket `ge-pbm-backups` under prefix `pbm`.

## Manual PBM commands

Logical backup:

```bash
docker exec backup-poc-pbm-agent pbm backup --type=logical
docker exec backup-poc-pbm-agent pbm list
```

Physical backup:

```bash
docker exec backup-poc-pbm-agent pbm backup --type=physical
docker exec backup-poc-pbm-agent pbm list
```

Restore:

```bash
docker exec backup-poc-pbm-agent pbm restore <backup-name> --yes
```

## Restore an imported mongodump archive into the lab

Restore an existing `mongodump --archive --gzip` file into `backup-poc-mongo` through the API UI, or manually:

```bash
docker cp /tmp/imported.archive.gz backup-poc-mongo:/tmp/imported.archive.gz
docker exec backup-poc-mongo mongorestore --uri=mongodb://localhost:27017/?replicaSet=rs0 --archive=/tmp/imported.archive.gz --gzip --drop
```

The API flow avoids the local `/tmp` file by streaming `mongodump` output into object storage and streaming object storage back into `mongorestore`.
