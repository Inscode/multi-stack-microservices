CREATE TABLE ORDERS (
    id BIGSERIAL PRIMARY KEY,
    customer_name VARCHAR(100) NOT NULL,
    total_amount NUMERIC(12,2) NOT NULL,
    status VARCHAR(30) NOT NULL,
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT now()
);