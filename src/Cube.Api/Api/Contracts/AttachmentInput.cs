namespace Cube.Api.Contracts;

public sealed record AttachmentInput(Guid FileId) : IApiRequest;
