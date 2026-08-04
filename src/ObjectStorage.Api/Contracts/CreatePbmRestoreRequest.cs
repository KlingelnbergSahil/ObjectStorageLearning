namespace ObjectStorage.Api.Contracts;

public sealed record CreatePbmRestoreRequest(
    string BackupName,
    bool DropExistingData = false,
    string? DatabaseNameToDrop = null);
