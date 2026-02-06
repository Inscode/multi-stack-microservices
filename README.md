# Mini Order System – Microservices Learning Project

This repository contains a **simple microservices-based system** built for learning and practical understanding of **backend development, microservice architecture, and DevOps fundamentals** using **multiple technology stacks**.

The project intentionally uses **three different backend frameworks** to demonstrate how heterogeneous microservices can coexist and communicate in a real-world system.

---

## 🧩 Architecture Overview

The system is composed of **three independent microservices**, each responsible for a single business capability:

| Service | Technology | Responsibility |
|------|-----------|---------------|
| Catalog Service | Django (Python) | Product and inventory management |
| Order Service | Spring Boot (Java) | Order creation and lifecycle |
| Payment Service | ASP.NET Core (.NET) | Payment processing |

Each service:
- Runs independently
- Owns its own database
- Communicates via REST APIs
- Can be deployed separately

---

## 🔁 High-Level Flow

1. **Catalog Service**
   - Manages products (name, price, stock)
2. **Order Service**
   - Creates orders
   - Fetches product details from Catalog Service
3. **Payment Service**
   - Processes payments
   - Updates order status to `PAID`

---

## 📁 Repository Structure

```text
mini-order-system/
├── catalog-service/     # Django + DRF (Product Catalog)
├── order-service/       # Spring Boot (Order Management)
├── payment-service/     # ASP.NET Core (Payment Processing)
├── infrastructure/      # Docker, Kubernetes, CI/CD (future)
├── docs/                # Architecture & API documentation
├── scripts/             # Helper scripts (local/dev)
├── README.md            # Project overview (this file)
└── .gitignore
