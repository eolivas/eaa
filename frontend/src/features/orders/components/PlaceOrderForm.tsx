import { type FormEvent, useState } from 'react';
import { usePlaceOrder } from '../api';
import type { PlaceOrderRequest } from '../types';

export function PlaceOrderForm() {
  const mutation = usePlaceOrder();

  const [customerId, setCustomerId] = useState('');
  const [productId, setProductId] = useState('');
  const [quantity, setQuantity] = useState('1');
  const [unitPrice, setUnitPrice] = useState('');
  const [currency, setCurrency] = useState('USD');

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    mutation.reset();

    const request: PlaceOrderRequest = {
      customerId,
      lines: [
        {
          productId,
          quantity: Number(quantity),
          unitPrice: Number(unitPrice),
          currency,
        },
      ],
    };

    mutation.mutate(request);
  }

  return (
    <form onSubmit={handleSubmit}>
      <div>
        <label htmlFor="customerId">Customer ID</label>
        <input
          id="customerId"
          type="text"
          value={customerId}
          onChange={(e) => setCustomerId(e.target.value)}
          required
        />
      </div>

      <div>
        <label htmlFor="productId">Product ID</label>
        <input
          id="productId"
          type="text"
          value={productId}
          onChange={(e) => setProductId(e.target.value)}
          required
        />
      </div>

      <div>
        <label htmlFor="quantity">Quantity</label>
        <input
          id="quantity"
          type="number"
          min="1"
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          required
        />
      </div>

      <div>
        <label htmlFor="unitPrice">Unit Price</label>
        <input
          id="unitPrice"
          type="number"
          min="0"
          step="0.01"
          value={unitPrice}
          onChange={(e) => setUnitPrice(e.target.value)}
          required
        />
      </div>

      <div>
        <label htmlFor="currency">Currency</label>
        <input
          id="currency"
          type="text"
          value={currency}
          onChange={(e) => setCurrency(e.target.value)}
          required
        />
      </div>

      {mutation.isError && <div role="alert">{mutation.error.message}</div>}

      <button type="submit" disabled={mutation.isPending}>
        Place Order
      </button>
    </form>
  );
}
