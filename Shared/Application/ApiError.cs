namespace FrioTrack.Api.Shared.Application;

public sealed class ApiError(string code, int status = 400) : Exception(code) {
    public string Code { get; } = code;
    public int Status { get; } = status;
}
