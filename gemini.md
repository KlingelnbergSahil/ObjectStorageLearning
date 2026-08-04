## Backup and File Management System Architecture## Context & Architectural Decisions Document (ADR)
This document provides a comprehensive summary of the architecture, technical decisions, and data workflows finalized for the unified database backup and application file management module.
------------------------------
## 1. Architectural Summary & Sizing Strategy
The system utilizes an adaptive, scale-based sizing strategy to optimize speed and protect system resources as data grows from a few megabytes to over 300 GB.

                  ┌─────────────────────────────────────────┐
                  │    GearEngine (C# API + Hangfire)       │
                  └────────────────────┬────────────────────┘
                                       │ 
                                       ▼
                  ┌─────────────────────────────────────────┐
                  │            DB Size Detector             │
                  └────┬───────────────┬────────────────┬───┘
                       │               │                │
             < 20 GB   │       20 GB – 100 GB           │   > 100 GB – 300 GB+
                       ▼               ▼                ▼
         ┌───────────────────┐ ┌───────────────┐ ┌──────────────────────┐
         │      Tier 1       │ │    Tier 2     │ │        Tier 3        │
         │    mongodump      │ │  PBM Logical  │ │     PBM Physical     │
         │   (Single-Node    │ │ (PBM Sidecar  │ │ (Full + Incremental) │
         │   Replica Set)    │ │   Required)   │ │  (PSMDB Engine Req)  │
         │  [--replSet rs0]  │ └───────┬───────┘ └──────────┬───────────┘
         └─────────┬─────────┘         │                    │
                   │                   │                    │
                   └───────────────────┼────────────────────┘
                                       │
                                       ▼
     ┌──────────────────────────────────────────────────────────────────┐
     │         Unified S3 Storage Abstraction (AWSSDK.S3)               │
     │   On-Premises: SeaweedFS   │   Cloud Production: Azure Blob      │
     └─────────────────────────────────┬────────────────────────────────┘
                                       │
            ┌──────────────────────────┴──────────────────────────┐
            ▼                                                     ▼
┌───────────────────────────────┐                     ┌───────────────────────────────┐
│     File Service Upload       │                     │    File Service Download      │
│  Browser S3 Multipart Upload  │                     │      S3 Presigned URLs        │
│ 0 MB C# RAM via Presigned PUT │                     │ 0 MB C# RAM via Presigned GET │
└───────────────────────────────┘                     └───────────────────────────────┘


* Tier 1: < 20 GB (Tiny Data)
* Tool: Standard logical mongodump and mongorestore.
   * Workflow: Simple, highly portable BSON dumps. Restores are fast enough at this scale that index rebuild overhead is negligible.
* Tier 2: 20 GB – 100 GB (Medium Data)
* Tool: Percona Backup for MongoDB (PBM) - Logical Mode.
   * Workflow: Captures a live database state while streaming files out of the container seamlessly, ensuring zero disruption to live operations.
* Tier 3: 100 GB – 300 GB+ (Large/Production Data)
* Tool: Percona Backup for MongoDB (PBM) - Physical/Incremental Mode.
   * Workflow: Bypasses document-by-document querying. Copies raw storage blocks directly from the WiredTiger storage engine. It provides a full weekly baseline snapshot followed by daily incremental changes, reducing data restore windows from 12+ hours (index rebuilding) to under 30 minutes.

------------------------------
## 2. Infrastructure & Storage Decisions## Decision 1: Mandatory MongoDB Replica Set Mode

* Context: Standard MongoDB containers run in "standalone" mode where the internal transaction log (Oplog) is completely disabled.
* Decision: All MongoDB container environments must run as a Single-Node Replica Set (using the configuration flag --replSet rs0).
* Rationale: Percona PBM relies entirely on the Oplog stream to snapshot blocks and maintain state consistency.

## Decision 2: Abstracted S3 Storage Layer

* Context: Hardcoding absolute Linux file directories (bind mounts) breaks container statelessness, prevents horizontal API scaling, and prevents native browser integration. Furthermore, PBM requires an S3 or Azure API target endpoint to function.
* Decision: Implement a Unified S3 API Abstraction Layer using environment variables to control the backing storage target securely.
* On-Premises Production: Deploy SeaweedFS in a container right next to the database. It is 100% free, Apache/MIT-licensed, actively patched open-source software that safely converts local server disks into an S3-compatible cloud storage endpoint. (MinIO Community Edition was explicitly rejected due to its repository archival state and AGPLv3 legal risks).
   * Cloud Production (Azure): Change environment variables to point directly to an Azure Blob Storage account container.
* Rationale: The C# business logic and Percona tooling interact entirely with the generic S3 API standard, making the entire platform 100% portable cross-environment with no application code changes.

## Decision 3: Drop tusdotnet & Nginx Proxies for File Transfers

* Context: Historically, large chunked transfers over public networks required complex middleman libraries like tusdotnet alongside specialized reverse-proxy tuning in Nginx.
* Decision: Leverage native S3 Multipart Browser Client Uploading and S3 Presigned URLs via the official frontend/backend SDKs for all application file operations.
* Rationale: This removes complex uploading protocols from the C# application code completely. The storage engine itself natively manages client connection drops, data block assembly, and resume states.

------------------------------
## 3. End-to-End Workflow Engineering## Workflow A: Database Backup Generation & Storage

   1. The administrator clicks "Backup Now" in the web UI, or Hangfire executes a routine cron script at 2:00 AM.
   2. The C# API queries the target database size metric.
   3. C# invokes an asynchronous control shell process against the PBM sidecar container: docker exec pbm_agent pbm backup --type=physical.
   4. The PBM Agent queries the WiredTiger block layer, compresses the database chunks on the fly, and uses its built-in S3 engine to stream data over the secure network straight to SeaweedFS / Azure Blob.
   5. C# Server Resource Cost: 0 MB of RAM and 0% file disk buffer allocation.

## Workflow B: Secure Backup File Downloads

   1. The user views the backup log dropdown array in the application UI and hits the "Download Archive" icon.
   2. The UI communicates with the C# backend module: GET /api/backup/download-url?filename=xyz.pbm.
   3. The C# code uses the local AWSSDK.S3 library to execute an offline cryptographic handshake. It returns an encrypted, secure S3 Presigned GET URL with a hardcoded timeout window (e.g., 15 minutes).
   4. The frontend UI catches the URL text string, and the user’s web browser pulls the file bytes directly from SeaweedFS or Azure Cloud Storage. The multi-gigabyte payload completely bypasses your web application containers.

## Workflow C: External File Uploads & Disaster Recovery Restores

   1. An engineer drops an external database backup archive (.pbm or .tar.gz) from their laptop directly onto the UI dashboard area.
   2. The browser application prompts the C# module for secure permission tokens: POST /api/backup/upload-url.
   3. The API calculates a secure, time-locked S3 Presigned PUT URL and pushes it back to the browser.
   4. The browser's native JavaScript S3 SDK cuts the file into small 5MB chunks and uploads them directly to SeaweedFS / Azure Blob Storage. If public internet or home Wi-Fi drops at 90%, the browser natively retains state and resumes upon reconnecting.
   5. Once the file hits 100% inside the storage bucket, the UI alerts the C# controller.
   6. The C# code triggers a catalog refresh sequence followed by a core wipe-and-restore instruction:
   
   docker exec pbm_agent pbm resync
   docker exec pbm_agent pbm restore <uploaded_file_name>
   
   7. Percona updates the cluster volumes natively from the storage server.

------------------------------
## 4. Final C# Code Blueprint & Project Structure
To maintain clean architectural boundaries and keep components decoupled, all backup logic is consolidated into a single, unified module:

📁 Features/BackupManagement/
│
├── 📁 Controllers/
│   └── BackupController.cs       # Endpoints for triggers, lists, and presigned links
│
├── 📁 Services/
│   ├── BackupOrchestrator.cs     # Size evaluation, mongodump execution, and PBM command parsing
│   └── S3TransferEngine.cs       # AWSSDK.S3 cryptography for Presigned PUT/GET generation
│
├── 📁 Jobs/
│   └── ScheduledBackupWorker.cs  # Hangfire job registration for recurring crons
│
└── 📁 Models/
    ├── BackupRecordDto.cs        # Simple text model to list dates, sizes, and labels in the UI
    └── StorageConfig.cs          # Environment mapping (Endpoint, Bucket Name, Target Secret Keys)

## Reference Implementation: Cryptographic Presigning Engine (S3TransferEngine.cs)

using Amazon.S3;using Amazon.S3.Model;using Microsoft.Extensions.Configuration;
namespace Features.BackupManagement.Services
{
    public class S3TransferEngine
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public S3TransferEngine(IAmazonS3 s3Client, IConfiguration config)
        {
            _s3Client = s3Client;
            _bucketName = config["STORAGE_BUCKET_NAME"] ?? "mongo-db-backups";
        }

        /// <summary>
        /// Generates a time-locked, tamper-proof signature URL string allowing the web client 
        /// browser to interface directly with the object storage engine using zero server RAM.
        /// </summary>
        public string GenerateSecurePresignedUrl(string fileName, HttpVerb operation, int expiresMinutes = 15)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                Verb = operation, // HttpVerb.GET for Downloads, HttpVerb.PUT for Uploads
                Expires = DateTime.UtcNow.AddMinutes(expiresMinutes)
            };

            // This calculates the signature locally via cryptographic math without making network calls
            return _s3Client.GetPreSignedURL(request);
        }
    }
}

------------------------------
## 5. Summary Matrix of Architectural Responsibilities

| Action | Execution Plane | Transfer Mechanism | C# API Role |
|---|---|---|---|
| Scheduled Core Backup | Percona PBM Agent / MongoDB | Native Database Block-to-S3 Stream | Hangfire Cron Trigger |
| Local File Download | Web Storage Server ➔ Client Browser | HTTP GET via Presigned Token URL | Crypto URL Generator |
| External Migration Upload | Client Browser ➔ Web Storage Server | S3 Client-Side Multipart Chunking | Crypto URL Generator |
| Disaster System Restore | Percona PBM Agent / MongoDB | S3 Chunks Overwritten to Disk Volume | Asynchronous CLI Invoker |

