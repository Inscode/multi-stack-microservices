namespace PaymentService.Contracts;

public record CreatePaymentRequest(int OrderId, decimal Amount);
