import { describe, it, expect } from 'vitest';
import { render, within } from '@testing-library/react';
import { LoadingIndicator } from './LoadingIndicator';

describe('LoadingIndicator', () => {
  it('sets aria-busy="true" on container when loading', () => {
    const { container } = render(
      <LoadingIndicator loading={true}>
        <p>Content</p>
      </LoadingIndicator>
    );
    const wrapper = container.firstElementChild as HTMLElement;
    expect(wrapper).toHaveAttribute('aria-busy', 'true');
  });

  it('sets aria-busy="false" on container when not loading', () => {
    const { container } = render(
      <LoadingIndicator loading={false}>
        <p>Content</p>
      </LoadingIndicator>
    );
    const wrapper = container.firstElementChild as HTMLElement;
    expect(wrapper).toHaveAttribute('aria-busy', 'false');
  });

  it('displays loading indicator text when loading', () => {
    const { container } = render(<LoadingIndicator loading={true} />);
    const wrapper = within(container.firstElementChild as HTMLElement);
    expect(wrapper.getByRole('status')).toBeInTheDocument();
    expect(wrapper.getByText('Loading…')).toBeInTheDocument();
  });

  it('hides loading indicator when not loading', () => {
    const { container } = render(<LoadingIndicator loading={false} />);
    const wrapper = within(container.firstElementChild as HTMLElement);
    expect(wrapper.queryByRole('status')).not.toBeInTheDocument();
    expect(wrapper.queryByText('Loading…')).not.toBeInTheDocument();
  });

  it('renders children regardless of loading state', () => {
    const { container, rerender } = render(
      <LoadingIndicator loading={true}>
        <p>Child content</p>
      </LoadingIndicator>
    );
    const wrapper = within(container.firstElementChild as HTMLElement);
    expect(wrapper.getByText('Child content')).toBeInTheDocument();

    rerender(
      <LoadingIndicator loading={false}>
        <p>Child content</p>
      </LoadingIndicator>
    );
    const wrapperAfter = within(container.firstElementChild as HTMLElement);
    expect(wrapperAfter.getByText('Child content')).toBeInTheDocument();
  });

  it('has an accessible label on the loading status element', () => {
    const { container } = render(<LoadingIndicator loading={true} />);
    const wrapper = within(container.firstElementChild as HTMLElement);
    const status = wrapper.getByRole('status');
    expect(status).toHaveAttribute('aria-label', 'Loading');
  });
});
