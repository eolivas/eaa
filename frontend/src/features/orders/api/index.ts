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
