namespace Cube.Api.Contracts;

public sealed record ReviewInput(string Status, string Reason) : IApiRequest;
