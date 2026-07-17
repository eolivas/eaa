# Bounded Context Specification

This document defines the bounded context boundaries for the platform's core services. Each bounded context is self-contained: it specifies its aggregate root(s), the domain events it publishes, and the domain events it subscribes to.

---

## Identity Service

### Aggregate Roots

| Aggregate | Description |
|-----------|-------------|
| **User** | Represents a platform user account. Manages registration, authentication credentials, and profile data. Enforces uniqueness of email/username and password-complexity rules. |

### Value Objects

| Value Object | Description |
|--------------|-------------|
| **Role** | Represents a user's authorization role (e.g., Admin, Member). Immutable; validated on construction. |

### Published Domain Events

| Event | Raised When |
|-------|-------------|
| **UserRegisteredEvent** | A new user account is successfully created via the registration flow. |
| **UserAuthenticatedEvent** | A user successfully completes authentication and a token is issued. |

### Subscribed Domain Events

The Identity service does not subscribe to domain events from other bounded contexts. It is an upstream authority that other services query for authentication and authorization decisions.

---

## Orders Service

### Aggregate Roots

| Aggregate | Description |
|-----------|-------------|
| **Order** | Represents a customer order. Contains one or more `OrderLine` entities and a computed `Total` (Money value object). Enforces the status lifecycle: `Pending → Placed → Shipped` and `Pending → Cancelled`, `Placed → Cancelled`. All invalid transitions are rejected. |

### Entities

| Entity | Description |
|--------|-------------|
| **OrderLine** | A line item within an Order. Holds a product reference, quantity (must be > 0), and unit price (Money). Created exclusively through a static factory method that enforces domain invariants. |

### Value Objects

| Value Object | Description |
|--------------|-------------|
| **Money** | Immutable representation of a currency amount. Rejects negative amounts and non-ISO-4217 currency codes. Supports addition and multiplication operators. |

### Published Domain Events

| Event | Raised When |
|-------|-------------|
| **OrderCreatedEvent** | `Order.Create(customerId, lines)` is called successfully, producing a new Order in `Pending` status. Carries `OrderId` and `CustomerId`. |
| **OrderPlacedEvent** | `Order.Place()` is called on an Order in `Pending` status, transitioning it to `Placed`. Carries `OrderId`. |
| **OrderCancelledEvent** | `Order.Cancel(reason)` is called on an Order in `Pending` or `Placed` status, transitioning it to `Cancelled`. Carries `OrderId` and `Reason`. |

### Subscribed Domain Events

The Orders service does not subscribe to domain events from other bounded contexts. It is a publisher of order-lifecycle events consumed by downstream services (e.g., Notifications).

---

## Notifications Service

### Aggregate Roots

The Notifications service does not define its own aggregate roots. It is an event-driven service that reacts to domain events published by other bounded contexts and dispatches notifications (email, SMS, push) through configured delivery channels.

### Published Domain Events

The Notifications service does not publish domain events. Its responsibility is to consume events and produce side effects (sending notifications) rather than modifying domain state.

### Subscribed Domain Events

| Event | Source Context | Action Taken on Receipt |
|-------|---------------|------------------------|
| **OrderPlacedEvent** | Orders | Triggers the `SendEmailNotification` handler, which composes and sends an order-confirmation email to the customer via the configured email delivery infrastructure (SMTP or Amazon SES). |

---

## Context Map Summary

```
┌──────────────────┐       publishes        ┌────────────────────────┐
│  Identity        │                         │  Orders                │
│  (upstream)      │                         │  (core domain)         │
│                  │                         │                        │
│  User aggregate  │                         │  Order aggregate       │
│  Role VO         │                         │  OrderLine entity      │
│                  │                         │  Money VO              │
│  Publishes:      │                         │                        │
│  UserRegistered  │                         │  Publishes:            │
│  UserAuthenticated│                        │  OrderCreatedEvent     │
└──────────────────┘                         │  OrderPlacedEvent      │
                                             │  OrderCancelledEvent   │
                                             └───────────┬────────────┘
                                                         │
                                              subscribes │ OrderPlacedEvent
                                                         ▼
                                             ┌────────────────────────┐
                                             │  Notifications         │
                                             │  (downstream)          │
                                             │                        │
                                             │  No aggregate roots    │
                                             │  Consumes events →     │
                                             │  sends notifications   │
                                             └────────────────────────┘
```
