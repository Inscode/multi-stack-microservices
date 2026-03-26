package order_service.project.service;

import jakarta.transaction.Transactional;
import lombok.RequiredArgsConstructor;
import order_service.project.domain.Order;
import order_service.project.dto.CreateOrderRequest;
import order_service.project.repository.OrderRepository;
import org.springframework.stereotype.Service;

import java.math.BigDecimal;
import java.time.Instant;

@Service
@RequiredArgsConstructor
public class OrderService {

    private final OrderRepository orderRepository;

    @Transactional
    public Order createOrder(CreateOrderRequest request) {
        // TODO: call Django catalog service via webclient to get actual price

        BigDecimal unitPrice = new BigDecimal("100.00");
        BigDecimal total = unitPrice.multiply(BigDecimal.valueOf(request.quantity()));

        Order order = Order.builder()
                .customerName(request.customerName())
                .totalAmount(total)
                .status("PENDING")
                .createdAtUtc(Instant.now())
                .build();

        return orderRepository.save(order);
    }

    public Order getOrderById(Long id) {
        return orderRepository.findById(id)
                .orElseThrow(() -> new RuntimeException("Order not found with id: " + id));
    }
}
