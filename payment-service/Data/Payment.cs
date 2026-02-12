namespace PaymentService.Data;

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "SUCCESS";
    public string IdempotencyKey {get; set;} = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}