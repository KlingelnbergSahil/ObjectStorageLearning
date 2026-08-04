namespace ObjectStorage.Api.Contracts;

public sealed record CommandResultResponse(
    int ExitCode,
    bool Succeeded,
    string StandardOutput,
    string StandardError);
