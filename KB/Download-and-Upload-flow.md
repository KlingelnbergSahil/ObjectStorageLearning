Here is the exact step-by-step technical process for both workflows, showing how data and control commands flow through your system from the moment the user clicks a button in your UI.
------------------------------
## Process 1: User Clicks "Backup" ➔ Downloads It to Local System
This workflow is broken into two distinct phases: Generating the backup (internal server-to-storage traffic) and Downloading the backup (direct storage-to-browser traffic).
## Phase A: Generating the Backup

   1. User Action: The admin opens your web UI, selects a target database from a dropdown menu, and clicks "Backup Now".
   2. API Request: The frontend UI sends a lightweight HTTP POST request to your C# API: POST /api/backup?db=production_db.
   3. Size Detection: Your C# API runs a quick metadata check on MongoDB to see how large the database is.
   * If < 20 GB: Your C# API bypasses Percona and triggers a shell script command running mongodump --archive --gzip directly targetting your storage block.
      * If > 20 GB: Your C# API triggers a lightweight command inside the PBM Agent sidecar container: docker exec pbm_agent pbm backup --type=physical.
   4. Data Streaming: The PBM Agent connects directly to MongoDB, reads the physical blocks, compresses them on the fly, and uses its internal S3 client to stream the chunks straight over the fast internal network into your Storage Vault (SeaweedFS on-prem or Azure Blob in the cloud).
   5. Completion: PBM finishes the upload and registers the backup name (e.g., 2026-08-03T120000Z.pbm). Your C# API logs the status as "Success" in your database and notifies the UI. (Your C# API used 0 MB of RAM during this entire process).

## Phase B: Downloading It to the Local System

   1. User Action: The admin sees the new backup appear in your UI history table and clicks the "Download" button.
   2. Link Request: The UI sends a fast text request to your C# API: GET /api/backup/download-url?filename=2026-08-03T120000Z.pbm.
   3. Presigning: Your C# API utilizes the AWSSDK.S3 library to calculate an S3 Presigned GET URL for that specific file inside your storage vault. This is an offline cryptographic operation that takes less than 1 millisecond.
   4. Handshake: The C# API sends this text URL string back to the browser.
   5. Direct Download: The user's browser opens this temporary link and downloads the multi-gigabyte file directly from SeaweedFS or Azure Blob Storage to their local hard drive. (The heavy file bytes completely bypass your C# API, saving your server from memory crashes).

------------------------------
## Process 2: Uploading an External File from Local System ➔ Restoring It
This workflow handles the reverse process when a user has a backup file sitting on their personal computer and wants to import and restore it.

   1. User Action: The admin opens your UI, selects a .tar.gz or .pbm backup file from their local laptop, and clicks "Upload and Restore".
   2. Permission Request: Before sending any data bytes, the browser UI asks your C# API for upload permission: POST /api/backup/upload-url?filename=external_migration.pbm.
   3. Presigning the Upload: Your C# API uses the AWSSDK.S3 library to generate an S3 Presigned PUT URL for that file destination inside your storage bucket and returns the link string to the browser.
   4. Direct Browser Upload: Your frontend JavaScript framework catches the URL and uses the standard S3 Browser SDK to start the upload. The browser automatically slices the large file into small, independent 5MB chunks and pushes them directly into SeaweedFS or Azure Blob Storage.
   * Network Drop Protection: If the user's home Wi-Fi drops at 75%, the browser SDK pauses. When the Wi-Fi reconnects, it automatically resumes uploading from 75%—completely bypassing your C# API container.
   5. Syncing Percona: Once the upload hits 100%, the browser UI notifies your C# API. Your API immediately fires a control command to Percona: docker exec pbm_agent pbm resync. This forces Percona's internal S3 client to scan the storage bucket, see the newly uploaded file, and register it in its catalog.
   6. Executing the Restore: Your C# API runs the final restore instruction against the sidecar: docker exec pbm_agent pbm restore external_migration.pbm.
   7. Database Overwrite: Percona pulls the backup blocks natively out of your storage vault and safely overwrites the MongoDB data files. Your database is now restored and back online.

------------------------------
## Summary Architectural Checklist for Your Implementation

* Are you ready to see the C# endpoints that handle generating these GET and PUT presigned URLs?
* Do you want the frontend JavaScript code that uses the presigned URL to slice and upload files straight from the browser?

I can provide the exact code blocks to match these two verified workflows perfectly.
