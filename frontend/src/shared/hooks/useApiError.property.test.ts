// Feature: template-architecture-gaps, Property 13: ProblemDetails Field Error Display
// **Validates: Requirements 14.2**

import { describe, it, expect } from 'vitest';
import * as fc from 'fast-check';
import { AxiosError, AxiosHeaders } from 'axios';
import { parseApiError } from './useApiError';

/**
 * Helper to create an AxiosError with a ProblemDetails response body.
 */
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

/**
 * Arbitrary that generates a non-empty string suitable for a field key.
 * Field keys in ProblemDetails errors can be property paths like "Lines[0].Quantity".
 */
const fieldKeyArb = fc.stringMatching(/^[a-zA-Z][a-zA-Z0-9_.[\]]{0,29}$/);

/**
 * Arbitrary that generates a non-empty array of non-empty error message strings.
 */
const errorMessagesArb = fc.array(
  fc.string({ minLength: 1, maxLength: 100 }),
  { minLength: 1, maxLength: 5 },
);

/**
 * Arbitrary that generates a ProblemDetails-like errors dictionary
 * with at least 1 field key, each having at least 1 error message.
 */
const errorsRecordArb = fc
  .array(fc.tuple(fieldKeyArb, errorMessagesArb), { minLength: 1, maxLength: 10 })
  .map((entries) => {
    const record: Record<string, string[]> = {};
    for (const [key, messages] of entries) {
      // Ensure unique keys by using first occurrence
      if (!(key in record)) {
        record[key] = messages;
      }
    }
    return record;
  })
  .filter((record) => Object.keys(record).length >= 1);

describe('Property 13: ProblemDetails Field Error Display', () => {
  it('parseApiError extracts at least the first validation error for each field key', () => {
    fc.assert(
      fc.property(errorsRecordArb, (errors) => {
        const problemDetails = {
          title: 'Validation Failed',
          detail: 'One or more validation errors occurred.',
          status: 400,
          errors,
        };

        const axiosError = makeAxiosError(problemDetails);
        const result = parseApiError(axiosError);

        // For every field key in the generated errors dictionary that has at least one message,
        // the parsed fieldErrors result must contain that key with the first error message.
        for (const [fieldKey, messages] of Object.entries(errors)) {
          if (messages.length > 0) {
            expect(result.fieldErrors[fieldKey]).toBe(messages[0]);
          }
        }
      }),
      { numRuns: 100 },
    );
  });
});
