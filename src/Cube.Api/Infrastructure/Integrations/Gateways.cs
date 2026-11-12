namespace Cube.Api;

// Contracts intentionally describe local business events, not a fabricated remote API.
// Implement a concrete provider after its documentation/authentication is available.
public record DeliveryResult(bool Delivered, string State, string? ExternalId = null);
public interface IBitrixGateway { Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken); }
public interface IAccountingGateway { Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken); }
public interface IPaymentGateway { Task<Uri> CreateCheckoutAsync(Invoice invoice, string idempotencyKey, CancellationToken cancellationToken); }
public interface IEmailGateway { Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken); }
public sealed class UnconfiguredIntegrations : IBitrixGateway, IAccountingGateway, IPaymentGateway, IEmailGateway
{
    public Task<DeliveryResult> DeliverAsync(Outbox message, CancellationToken cancellationToken) => Task.FromResult(new DeliveryResult(false, "AwaitingConfiguration"));
    public Task<Uri> CreateCheckoutAsync(Invoice invoice, string idempotencyKey, CancellationToken cancellationToken) => throw new ApiError(503, "payment_provider_not_configured");
}
