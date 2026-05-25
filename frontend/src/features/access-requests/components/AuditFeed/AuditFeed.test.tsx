import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { FC, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AdminAuthProvider, adminAuthKeys } from "@/features/admin-auth";
import { AuditFeed } from ".";

const aggregateId = "22222222-2222-4222-8222-222222222222";

const firstPageResponse = {
  items: [
    {
      id: "11111111-1111-4111-8111-111111111111",
      eventType: "session.revoked",
      aggregateId,
      occurredAt: "2026-05-23T10:15:00Z",
      details: { actor: "admin", reason: "manual revoke" },
    },
  ],
  nextCursor: "cursor-next-page",
};

const emptyResponse = {
  items: [],
  nextCursor: null,
};

interface TestProvidersProps {
  readonly authenticated?: boolean;
  readonly children: ReactNode;
}

const TestProviders: FC<TestProvidersProps> = ({ authenticated = true, children }) => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  });
  queryClient.setQueryData(adminAuthKeys.me, {
    authenticated,
    username: authenticated ? "admin" : "",
  });
  return (
    <QueryClientProvider client={queryClient}>
      <AdminAuthProvider>{children}</AdminAuthProvider>
    </QueryClientProvider>
  );
};

const renderAuditFeed = (authenticated = true): void => {
  render(
    <TestProviders authenticated={authenticated}>
      <AuditFeed.Root>
        <AuditFeed.Filters />
        <AuditFeed.ErrorState />
        <AuditFeed.EmptyState />
        <AuditFeed.List />
        <AuditFeed.Pagination />
      </AuditFeed.Root>
    </TestProviders>,
  );
};

const getLastFetchUrl = (
  calls: ReadonlyArray<readonly [RequestInfo | URL, RequestInit?]>,
): string => {
  const lastCall = calls[calls.length - 1];
  if (lastCall === undefined) {
    return "";
  }
  return lastCall[0].toString();
};

describe("AuditFeed", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("renders an admin login warning and does not fetch without a session", (): void => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);

    renderAuditFeed(false);

    expect(screen.getByText(/Melde dich als Admin an/)).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("loads audit events with an admin session and shows event type, aggregate id, and details", async (): Promise<void> => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.resolve(new Response(JSON.stringify(firstPageResponse)))),
    );

    renderAuditFeed();

    expect(await screen.findByText("session.revoked")).toBeInTheDocument();
    expect(screen.getByText(aggregateId)).toBeInTheDocument();
    expect(screen.getByText("actor")).toBeInTheDocument();
    expect(screen.getByText("admin")).toBeInTheDocument();
    expect(screen.getByText("reason")).toBeInTheDocument();
    expect(screen.getByText("manual revoke")).toBeInTheDocument();
  });

  it("encodes aggregateId, eventType, from, and to filter controls in the query", async (): Promise<void> => {
    const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>(
      (_input, _init) => Promise.resolve(new Response(JSON.stringify(emptyResponse))),
    );
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    renderAuditFeed();

    await screen.findByText(/No audit events match/);
    await user.type(screen.getByLabelText("Aggregate ID"), aggregateId);
    await user.type(screen.getByLabelText("Event type"), "session.revoked");
    await user.type(screen.getByLabelText("From"), "2026-05-23T10:00:00Z");
    await user.type(screen.getByLabelText("To"), "2026-05-23T11:00:00Z");

    await waitFor(() => {
      const url = getLastFetchUrl(fetchMock.mock.calls);
      expect(url).toContain(`aggregateId=${aggregateId}`);
      expect(url).toContain("eventType=session.revoked");
      expect(url).toContain("from=2026-05-23T10%3A00%3A00Z");
      expect(url).toContain("to=2026-05-23T11%3A00%3A00Z");
    });
  });

  it("uses nextCursor when loading the next page", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => {
      const url = input.toString();
      if (url.includes("cursor=cursor-next-page")) {
        return Promise.resolve(new Response(JSON.stringify(emptyResponse)));
      }
      return Promise.resolve(new Response(JSON.stringify(firstPageResponse)));
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    renderAuditFeed();

    await screen.findByText("session.revoked");
    await user.click(screen.getByRole("button", { name: "Next page" }));

    await waitFor(() => {
      const url = getLastFetchUrl(fetchMock.mock.calls);
      expect(url).toContain("cursor=cursor-next-page");
    });
  });

  it("shows the empty state when no items are returned", async (): Promise<void> => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.resolve(new Response(JSON.stringify(emptyResponse)))),
    );

    renderAuditFeed();

    expect(await screen.findByText(/No audit events match/)).toBeInTheDocument();
  });
});
