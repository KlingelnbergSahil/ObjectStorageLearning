# Backup Strategy Comparison

This document compares the three restore/backup flows used in this POC:

1. `mongodump` / `mongorestore`
2. Percona Backup for MongoDB logical backup
3. Percona Backup for MongoDB physical backup

## Quick Comparison

| Area | `mongodump` / `mongorestore` | PBM logical | PBM physical |
|---|---|---|---|
| Backup type | Logical export | Logical PBM snapshot | Physical storage-file snapshot |
| Restore type | Logical import | PBM logical restore | PBM physical restore |
| Main command | `mongodump`, `mongorestore` | `pbm backup --type=logical`, `pbm restore` | `pbm backup --type=physical`, `pbm restore` |
| Data format | BSON dump archive or dump folder | PBM-managed objects and metadata in S3 | PBM-managed database files and metadata in S3 |
| Output shape | One archive file, gzipped archive, or folder | Multiple S3 objects under `pbm/<snapshot-name>/` | Multiple S3 objects under `pbm/<snapshot-name>/` |
| Restore selector | File path, archive stream, or storage object key | PBM snapshot name | PBM snapshot name |
| Works with normal MongoDB image | Yes | Yes, for supported replica sets | No for physical backup source; requires Percona Server for MongoDB support |
| Needs replica set | Not strictly for simple dump/restore | Yes, PBM works against replica sets/sharded clusters | Yes |
| Good for selected DB/file upload | Yes | Better for PBM-managed snapshots, not user archive upload | No, whole-deployment style restore |
| Good for very large DB restore | Can be slow | Can be slow compared with physical | Best option among these |
| PITR path | No built-in PITR | PBM PITR supported | PBM PITR supported depending on PBM mode/version/config |
| Operational complexity | Lowest | Medium | Highest |

## What Actually Runs

### `mongodump` / `mongorestore`

This flow runs MongoDB Database Tools directly.

Backup example:

```bash
mongodump --uri mongodb://backup-poc-mongo:27017 --db R300 --archive --gzip
```

Restore example:

```bash
mongorestore --uri mongodb://backup-poc-mongo:27017 --archive --gzip --drop
```

The POC wraps this flow in the API and stores the generated archive in SeaweedFS S3.

Use this when:

- You want a single downloadable/uploadable backup file.
- You want to restore from a user-provided `.archive` or `.archive.gz`.
- You want to move one DB between environments.
- You want simple learning and debugging.

Tradeoff:

- Restore can be slower because data is inserted back logically and indexes/data structures may need work.
- You must manage catalog, upload/download, retention, and scheduling yourself.

## PBM Logical

PBM logical is also a logical backup, but PBM manages the backup lifecycle.

Backup:

```bash
pbm backup --type=logical
```

Restore:

```bash
pbm restore <snapshot-name> --yes
```

PBM logical does not produce one normal `mongodump` archive. It writes a PBM snapshot into configured storage. In this POC, that storage is SeaweedFS S3:

```text
ge-pbm-backups/pbm/<snapshot-name>/
```

Use this when:

- You want production-style PBM backup orchestration.
- You want PBM status/list/history.
- You want direct S3 integration from PBM.
- You want a replica-set aware backup workflow.
- You want to learn PITR later.

Tradeoff:

- It is not simply a user-uploaded file restore flow.
- Restore is by PBM snapshot name.
- It may not be significantly faster than `mongodump`, because both are logical backup approaches.

## PBM Physical

PBM physical copies database storage files instead of exporting documents.

Backup:

```bash
pbm backup --type=physical
```

Restore:

```bash
pbm restore <snapshot-name> --yes
```

Conceptually:

```text
Logical:  collections/documents -> BSON/export -> restore inserts documents
Physical: WiredTiger/dbPath files -> storage -> restore files back to dbPath
```

Use this when:

- The DB is large.
- Restore speed matters.
- You need whole-deployment disaster recovery.
- You are using Percona Server for MongoDB and PBM-supported versions.

Tradeoff:

- More destructive and operationally heavy.
- It restores the database storage state, not a single uploaded archive.
- It is not the right tool for casual single-file import/export.
- It can require extra post-restore steps.

## Expected Performance

| Scenario | Expected result |
|---|---|
| Small DB | Difference may be small. `mongodump` is simpler. |
| 10-15 GB DB | PBM logical and `mongodump` may be in a similar range; exact result depends on data/index/storage. |
| Very large DB | PBM physical usually has the biggest restore-time advantage. |
| User file upload/download speed test | `mongodump` archive flow is the cleanest. |
| PBM snapshot download speed test | Use POC `Download Bundle`, which streams the PBM S3 prefix as a ZIP. |

For your current restored R300-sized test DB, compare real numbers from the Blazor timing log:

1. Run `Mongodump Backup To SeaweedFS`.
2. Run PBM logical backup.
3. Run PBM physical backup.
4. Restore each flow after dropping data.
5. Compare duration, backup size, and whether R300 app works after restore.

## Migration Version Risk

All three restore methods restore DB state from backup time.

If the app migration version is stored in MongoDB, that migration value is restored too.

Safe test rule:

```text
Restore backup -> verify migration/version collection -> start matching R300 app version
```

Be careful with:

- old backup + newer app
- newer backup + older app
- running app while destructive restore is happening
- PBM physical restore on a non-disposable database

## How This POC Maps The Flows

| POC page | Flow | What to test |
|---|---|---|
| Mongodump | `mongodump` / `mongorestore` | Archive backup, upload, register, download, restore with drop |
| PBM Logical | PBM logical | PBM snapshot creation, snapshot list, bundle download, restore with optional DB drop |
| PBM Physical | PBM physical | Physical snapshot creation, bundle download, restore against disposable Percona Mongo |

## Practical Decision

For this learning POC:

```text
Use mongodump flow to understand file handling: upload, download, direct archive restore.
Use PBM logical to understand PBM-managed S3 snapshots and restore orchestration.
Use PBM physical to understand the speed/DR benefit for large database restore.
```

For actual production design:

```text
Small/simple import-export: mongodump/mongorestore
Managed replica-set backup with PITR direction: PBM logical
Large whole-cluster restore / DR: PBM physical
```

## Official References

- MongoDB Database Tools: https://www.mongodb.com/docs/database-tools/
- MongoDB `mongorestore`: https://www.mongodb.com/docs/database-tools/mongorestore/
- Percona backup and restore types: https://docs.percona.com/percona-backup-mongodb/features/backup-types.html
- Percona physical backups and restores: https://docs.percona.com/percona-backup-mongodb/features/physical.html
