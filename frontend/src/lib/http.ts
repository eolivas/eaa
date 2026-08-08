import axios, { AxiosError } from 'axios';
import { useAuthStore } from './auth-store';

/**
 * Sentinel message used to identify network errors (no HTTP response received).
 */
export const NETWORK_ERROR_MESSAGE = 'Unable to reach the server. Please check your connection.';

/**
 * Checks whether an error is a network error (no HTTP response).
 */
export function isNetworkError(error: unknown): boolean {
  return error instanceof AxiosError && !error.response;
}

const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
});

http.interceptors.request.use((config) => {
  const token = useAuthStore.getState().token;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

http.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error instanceof AxiosError) {
      // 401: clear auth store and redirect to login
      if (error.response?.status === 401) {
        useAuthStore.getState().clearToken();
        window.location.href = '/login';
        return Promise.reject(error);
      }

      // Network error: no response received (timeout, DNS failure, server unreachable)
      if (!error.response) {
        error.message = NETWORK_ERROR_MESSAGE;
        return Promise.reject(error);
      }
    }

    return Promise.reject(error);
  }
);

export default http;
