package order_service.project.dto;

import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

public record CreateOrderRequest (
    @NotBlank(message = "Customer name is required")
    String customerName,

    @NotNull(message = "Product ID is required")
    Long productId,

    @Min(value = 1, message = "Quantity must be at least 1")
    int quantity
) {}
