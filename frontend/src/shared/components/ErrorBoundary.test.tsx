import { render, cleanup } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ErrorBoundary } from './ErrorBoundary';

function ThrowingComponent({ error }: { error: Error }): JSX.Element {
  throw error;
}

function GoodComponent() {
  return <p>All is well</p>;
}

describe('ErrorBoundary', () => {
  beforeEach(() => {
    // Suppress React error boundary console.error noise in test output
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  it('renders children when no error occurs', () => {
    const { getByText } = render(
      <ErrorBoundary>
        <GoodComponent />
      </ErrorBoundary>,
    );

    expect(getByText('All is well')).toBeInTheDocument();
  });

  it('renders fallback UI with role="alert" when a child throws', () => {
    const { getByRole, getByText } = render(
      <ErrorBoundary>
        <ThrowingComponent error={new Error('Test crash')} />
      </ErrorBoundary>,
    );

    const alert = getByRole('alert');
    expect(alert).toBeInTheDocument();
    expect(getByText('Something went wrong')).toBeInTheDocument();
  });

  it('renders a reload button in fallback UI', () => {
    const { getByRole } = render(
      <ErrorBoundary>
        <ThrowingComponent error={new Error('Test crash')} />
      </ErrorBoundary>,
    );

    const reloadButton = getByRole('button', { name: /reload/i });
    expect(reloadButton).toBeInTheDocument();
  });

  it('calls window.location.reload when reload button is clicked', () => {
    const reloadMock = vi.fn();
    Object.defineProperty(window, 'location', {
      value: { reload: reloadMock },
      writable: true,
    });

    const { getByRole } = render(
      <ErrorBoundary>
        <ThrowingComponent error={new Error('Test crash')} />
      </ErrorBoundary>,
    );

    getByRole('button', { name: /reload/i }).click();
    expect(reloadMock).toHaveBeenCalledOnce();
  });
});
