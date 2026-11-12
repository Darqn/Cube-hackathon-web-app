namespace Cube.Api;

public sealed class ApiError(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
}
