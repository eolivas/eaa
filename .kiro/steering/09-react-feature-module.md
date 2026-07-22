---
inclusion: auto
---

# React Feature Module

All frontend features live in `frontend/src/features/{feature-name}/`. Follow this structure for new features.

## Feature Directory Structure

```
frontend/src/features/{feature-name}/
├── api/
│   └── index.ts         # TanStack Query hooks (useQuery, useMutation)
├── components/
│   ├── index.ts         # Barrel export for components
│   └── {Component}.tsx  # Feature-specific React components
├── hooks/
│   └── index.ts         # Feature-specific custom hooks
├── types.ts             # TypeScript interfaces and types
└── index.ts             # Feature barrel export
```

## Barrel Exports

Every directory needs an `index.ts` barrel file:

```typescript
// features/{name}/index.ts
export * from './types';
// Re-export components/hooks as needed for external use
```

## Types (`types.ts`)

Define all feature-specific interfaces:

```typescript
export interface OrderLineDto {
  productId: string;
  quantity: number;
  unitPrice: number;
  currency: string;
}

export interface OrderDto {
  id: string;
  customerId: string;
  status: string;
  lines: OrderLineDto[];
  total: { amount: number; currency: string };
}

export interface PlaceOrderRequest {
  customerId: string;
  lines: { productId: string; quantity: number; unitPrice: number; currency: string }[];
}
```

## API Layer (`api/index.ts`) — TanStack Query Hooks

```typescript
import { useQuery, useMutation } from '@tanstack/react-query';
import http from '../../../lib/http';
import type { OrderDto, PlaceOrderRequest } from '../types';

export function useOrder(id: string) {
  return useQuery<OrderDto>({
    queryKey: ['orders', id],
    queryFn: async () => {
      const response = await http.get<OrderDto>(`/orders/${id}`);
      return response.data;
    },
  });
}

export function usePlaceOrder() {
  return useMutation<OrderDto, Error, PlaceOrderRequest>({
    mutationFn: async (request: PlaceOrderRequest) => {
      const response = await http.post<OrderDto>('/orders', request);
      return response.data;
    },
  });
}
```

Rules:
- Import `http` from `../../../lib/http` (Axios instance with auth interceptor)
- Use `useQuery` for reads, `useMutation` for writes
- Query keys: `['{resource}', id]` for single items, `['{resource}']` for lists
- Type parameters: `useQuery<TData>`, `useMutation<TData, TError, TVariables>`

## Components

```tsx
import { type FormEvent, useState } from 'react';
import { usePlaceOrder } from '../api';
import type { PlaceOrderRequest } from '../types';

export function PlaceOrderForm() {
  const mutation = usePlaceOrder();
  // ... state and handlers

  return (
    <form onSubmit={handleSubmit}>
      {/* Use htmlFor on labels, id on inputs for accessibility */}
      <label htmlFor="customerId">Customer ID</label>
      <input id="customerId" type="text" required />
      {mutation.isError && <div role="alert">{mutation.error.message}</div>}
      <button type="submit" disabled={mutation.isPending}>Submit</button>
    </form>
  );
}
```

Rules:
- Named exports (no default exports)
- PascalCase component file names
- Use `mutation.isPending` / `mutation.isError` for loading/error states
- Accessibility: `htmlFor`, `role="alert"`, semantic HTML

## Zustand Stores

For feature-local state (not server state), use Zustand:

```typescript
import { create } from 'zustand';

interface CartState {
  items: CartItem[];
  addItem: (item: CartItem) => void;
  removeItem: (productId: string) => void;
  clear: () => void;
}

export const useCartStore = create<CartState>((set) => ({
  items: [],
  addItem: (item) => set((state) => ({ items: [...state.items, item] })),
  removeItem: (productId) =>
    set((state) => ({ items: state.items.filter((i) => i.productId !== productId) })),
  clear: () => set({ items: [] }),
}));
```

Rules:
- Place stores in `hooks/` or feature root
- Naming: `use{Feature}Store`
- Use `create<TState>` with typed interface
- Global stores (auth, theme) live in `src/lib/`

## Shared Libraries (`frontend/src/lib/`)

- `http.ts` — Axios instance with base URL `/api` and auth token interceptor
- `auth-store.ts` — Global Zustand store for JWT token
- `index.ts` — Barrel export

## Tech Stack Reference

| Concern | Library | Version |
|---------|---------|---------|
| HTTP | Axios | ^1.x |
| Server state | @tanstack/react-query | ^5.x |
| Client state | Zustand | ^5.x |
| Build | Vite | ^6.x |
| Testing | Vitest + @testing-library/react | — |
| Type checking | TypeScript | ~5.6 |
