import type { ReactNode } from 'react';

interface LoadingIndicatorProps {
  /** Whether the content is currently loading. */
  loading: boolean;
  /** Content to render inside the loading container. */
  children?: ReactNode;
}

/**
 * A wrapper component that applies `aria-busy="true"` to its container
 * when loading, and displays a visible loading indicator.
 *
 * Satisfies accessibility requirements by communicating loading state
 * to assistive technologies via the aria-busy attribute.
 */
export function LoadingIndicator({ loading, children }: LoadingIndicatorProps) {
  return (
    <div aria-busy={loading}>
      {loading && (
        <div role="status" aria-label="Loading">
          <span>Loading…</span>
        </div>
      )}
      {children}
    </div>
  );
}
