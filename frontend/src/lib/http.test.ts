import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { useAuthStore } from './auth-store';
import { isNetworkError, NETWORK_ERROR_MESSAGE } from './http';

describe('http interceptors', () => {
  const originalLocation = window.location;

  beforeEach(() => {
    useAuthStore.setState({ token: 'test-token' });
    // Mock window.location.href
    Object.defineProperty(window, 'location', {
      writable: true,
      value: { ...originalLocation, href: '' },
    });
  });

  afterEach(() => {
    useAuthStore.setState({ token: null });
    Object.defineProperty(window, 'location', {
      writable: true,
      value: originalLocation,
    });
  });

  describe('401 response handling', () => {
    it('clears auth store on 401', async () => {
      useAuthStore.setState({ token: 'my-secret-token' });

      // Dynamically import http to trigger interceptors
      const { default: http } = await import('./http');

      // Simulate a 401 response via adapter
      const mockAdapter = vi.fn().mockRejectedValue(
        new AxiosError(
          'Unauthorized',
          '401',
          undefined,
          {},
          {
            status: 401,
            statusText: 'Unauthorized',
            headers: {},
            config: { headers: new AxiosHeaders() },
            data: {},
          }
        )
      );
      http.defaults.adapter = mockAdapter;

      await expect(http.get('/test')).rejects.toThrow();
      expect(useAuthStore.getState().token).toBeNull();
    });

    it('redirects to /login on 401', async () => {
      const { default: http } = await import('./http');

      const mockAdapter = vi.fn().mockRejectedValue(
        new AxiosError(
          'Unauthorized',
          '401',
          undefined,
          {},
          {
            status: 401,
            statusText: 'Unauthorized',
            headers: {},
            config: { headers: new AxiosHeaders() },
            data: {},
          }
        )
      );
      http.defaults.adapter = mockAdapter;

      await expect(http.get('/test')).rejects.toThrow();
      expect(window.location.href).toBe('/login');
    });
  });

  describe('network error handling', () => {
    it('sets NETWORK_ERROR_MESSAGE when no response is received', async () => {
      const { default: http } = await import('./http');

      const networkError = new AxiosError(
        'Network Error',
        'ERR_NETWORK',
        undefined,
        {} // request present but no response
      );

      const mockAdapter = vi.fn().mockRejectedValue(networkError);
      http.defaults.adapter = mockAdapter;

      await expect(http.get('/test')).rejects.toMatchObject({
        message: NETWORK_ERROR_MESSAGE,
      });
    });
  });
});

describe('isNetworkError', () => {
  it('returns true for AxiosError without response', () => {
    const error = new AxiosError('Network Error', 'ERR_NETWORK');
    expect(isNetworkError(error)).toBe(true);
  });

  it('returns false for AxiosError with response', () => {
    const error = new AxiosError('Not Found', '404', undefined, {}, {
      status: 404,
      statusText: 'Not Found',
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: {},
    });
    expect(isNetworkError(error)).toBe(false);
  });

  it('returns false for non-AxiosError', () => {
    expect(isNetworkError(new Error('generic'))).toBe(false);
    expect(isNetworkError(null)).toBe(false);
    expect(isNetworkError('string')).toBe(false);
  });
});

describe('NETWORK_ERROR_MESSAGE', () => {
  it('has the expected user-facing message', () => {
    expect(NETWORK_ERROR_MESSAGE).toBe('Unable to reach the server. Please check your connection.');
  });
});
