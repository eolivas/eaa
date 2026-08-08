import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { parseApiError } from './useApiError';

function makeAxiosError(data: unknown, status = 400): AxiosError {
  const error = new AxiosError('Request failed', 'ERR_BAD_REQUEST', undefined, undefined, {
    data,
    status,
    statusText: 'Bad Request',
    headers: {},
    config: { headers: new AxiosHeaders() },
  });
  return error;
}

describe('parseApiError', () => {
  it('returns empty result for null input', () => {
    const result = parseApiError(null);
    expect(result.message).toBeNull();
    expect(result.fieldErrors).toEqual({});
    expect(result.problemDetails).toBeNull();
  });

  it('returns empty result for non-AxiosError', () => {
    const result = parseApiError(new Error('generic'));
    expect(result.message).toBeNull();
    expect(result.fieldErrors).toEqual({});
    expect(result.problemDetails).toBeNull();
  });

  it('returns empty result for AxiosError without response (network error)', () => {
    const error = new AxiosError('Network Error', 'ERR_NETWORK');
    const result = parseApiError(error);
    expect(result.message).toBeNull();
    expect(result.problemDetails).toBeNull();
  });

  it('returns empty result when response data is not ProblemDetails', () => {
    const error = makeAxiosError({ foo: 'bar' });
    const result = parseApiError(error);
    expect(result.message).toBeNull();
    expect(result.problemDetails).toBeNull();
  });

  it('parses detail field as message', () => {
    const error = makeAxiosError({
      title: 'Validation Failed',
      detail: 'One or more fields are invalid.',
      status: 400,
    });
    const result = parseApiError(error);
    expect(result.message).toBe('One or more fields are invalid.');
    expect(result.problemDetails?.title).toBe('Validation Failed');
  });

  it('falls back to title when detail is absent', () => {
    const error = makeAxiosError({
      title: 'Not Found',
      status: 404,
    });
    const result = parseApiError(error);
    expect(result.message).toBe('Not Found');
  });

  it('extracts first validation error per field from errors dictionary', () => {
    const error = makeAxiosError({
      title: 'Validation Failed',
      detail: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        'Lines[0].Quantity': ['Quantity must be at least 1.', 'Another error.'],
        'Lines[0].UnitPrice': ['UnitPrice must be greater than 0.'],
      },
    });
    const result = parseApiError(error);
    expect(result.fieldErrors['Lines[0].Quantity']).toBe('Quantity must be at least 1.');
    expect(result.fieldErrors['Lines[0].UnitPrice']).toBe('UnitPrice must be greater than 0.');
  });

  it('ignores empty error arrays in errors dictionary', () => {
    const error = makeAxiosError({
      title: 'Validation Failed',
      errors: {
        Name: [],
        Email: ['Email is required.'],
      },
    });
    const result = parseApiError(error);
    expect(result.fieldErrors['Name']).toBeUndefined();
    expect(result.fieldErrors['Email']).toBe('Email is required.');
  });

  it('returns full problemDetails object', () => {
    const problemData = {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'Bad Request',
      status: 400,
      detail: 'Invalid input.',
      instance: '/api/orders',
    };
    const error = makeAxiosError(problemData);
    const result = parseApiError(error);
    expect(result.problemDetails).toEqual(problemData);
  });
});
