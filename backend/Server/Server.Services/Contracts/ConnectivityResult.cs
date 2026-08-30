namespace Server.Services.Contracts;

public sealed record ConnectivityResult(
    string Status,
    long DurationMilliseconds,
    IReadOnlyList<ConnectivityStage> Stages,
    HttpResponsePreview? HttpResponse = null);
