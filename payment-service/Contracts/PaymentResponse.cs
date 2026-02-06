namespace PaymentService.Contracts;

public record PaymentResponse(int Id, int OrderId, decimal Amount, string Status, DateTime CreatedAtUtc);