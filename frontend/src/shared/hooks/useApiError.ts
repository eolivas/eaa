import { useMemo } from 'react';
import { AxiosError } from 'axios';

/**
 * RFC 9457 ProblemDetails shape returned by the .NET API.
 */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export interface ApiErrorResult {
  /** Human-readable error message: `detail` if available, otherwise `title`. */
  message: string | null;
  /** Per-field validation errors keyed by field name, first error per field. */
  fieldErrors: Record<string, string>;
  /** The full ProblemDetails object, or null if the error is not a ProblemDetails response. */
  problemDetails: ProblemDetails | null;
}

/**
 * Determines whether the given value looks like a ProblemDetails response body.
 */
function isProblemDetails(data: unknown): data is ProblemDetails {
  if (data == null || typeof data !== 'object') return false;
  const obj = data as Record<string, unknown>;
  return typeof obj.title === 'string' || typeof obj.detail === 'string';
}

/**
 * Parses an Axios error into a structured ProblemDetails result.
 * Returns null values when the error doesn't contain a ProblemDetails body.
 */
export function parseApiError(error: unknown): ApiErrorResult {
  const empty: ApiErrorResult = { message: null, fieldErrors: {}, problemDetails: null };

  if (!(error instanceof AxiosError) || !error.response) {
    return empty;
  }

  const data = error.response.data;
  if (!isProblemDetails(data)) {
    return empty;
  }

  const message = data.detail || data.title || null;

  const fieldErrors: Record<string, string> = {};
  if (data.errors) {
    for (const [field, messages] of Object.entries(data.errors)) {
      if (Array.isArray(messages) && messages.length > 0) {
        const first = messages[0];
        if (first !== undefined) {
          fieldErrors[field] = first;
        }
      }
    }
  }

  return { message, fieldErrors, problemDetails: data };
}

/**
 * Hook that parses an Axios error (or null/undefined) into a ProblemDetails result.
 * Memoizes the result to avoid unnecessary re-renders.
 *
 * @param error - An Axios error from a catch block or query error state, or null/undefined.
 * @returns Parsed ProblemDetails with message and field errors.
 */
export function useApiError(error: unknown): ApiErrorResult {
  return useMemo(() => parseApiError(error), [error]);
}
