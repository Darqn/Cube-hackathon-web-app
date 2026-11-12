namespace Cube.Api;

public static class NewsResponses
{
    public static object Dto(News n) => new { n.Id, n.Title, n.Summary, n.Body, n.Category, n.Importance, n.PublishAt, n.ExpiresAt, n.RequiresAcknowledgement };
}
