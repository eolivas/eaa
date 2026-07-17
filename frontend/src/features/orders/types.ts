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
