namespace Cube.Api.Modules.Requests.Domain;

public static class RequestRules
{
    public static readonly string[] RequestStates = ["New", "Accepted", "InProgress", "Clarification", "Completed", "Closed", "Rejected"];
    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        ("New", "Accepted" or "Rejected") => true,
        ("Accepted", "InProgress" or "Rejected") => true,
        ("InProgress", "Clarification" or "Completed" or "Rejected") => true,
        ("Clarification", "InProgress" or "Rejected") => true,
        ("Completed", "Closed" or "InProgress") => true,
        _ => false
    };
}
