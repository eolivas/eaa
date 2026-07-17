import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const queryClient = new QueryClient();

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <div>
        <h1>EAA Frontend</h1>
        <p>Enterprise Application Architecture Reference</p>
      </div>
    </QueryClientProvider>
  );
}
