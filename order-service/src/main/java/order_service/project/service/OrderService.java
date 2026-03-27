package order_service.project.service;

import jakarta.transaction.Transactional;
import lombok.RequiredArgsConstructor;
import order_service.project.config.WebClientConfig;
import order_service.project.domain.Order;
import order_service.project.dto.CreateOrderRequest;
import order_service.project.dto.ProductDto;
import order_service.project.repository.OrderRepository;
import org.springframework.stereotype.Service;
import org.springframework.web.reactive.function.client.WebClient;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;

@Service
@RequiredArgsConstructor
public class OrderService {

    private final OrderRepository orderRepository;
    private final WebClient catalogWebClient;

    @Transactional
    public Order createOrder(CreateOrderRequest request) {
        // TODO: call Django catalog service via webclient to get actual price

//        BigDecimal unitPrice = new BigDecimal("100.00");

        ProductDto product = catalogWebClient.get()
                .uri("/{id}", request.productId())
                .retrieve()
                .bodyToMono(ProductDto.class)
                .block();

        if (product == null) {
            throw new RuntimeException("Product not found in catalog service");
        }
        BigDecimal total = product.price().multiply(BigDecimal.valueOf(request.quantity()));

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

    public List<Order> getAllOrders() {
        return orderRepository.findAll();
    }
}
