import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { FC, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AdminAuthGate, AdminAuthProvider, adminAuthKeys } from "@/features/admin-auth";
import { AccessRequestDashboard } from ".";

const requestId = "11111111-1111-4111-8111-111111111111";
const sessionId = "22222222-2222-4222-8222-222222222222";

const listResponse = {
  items: [
    {
      id: requestId,
      intent: "Inspect service logs",
      requester: "agent-alpha",
      targets: ["homelab.logs"],
      requestedCapabilities: ["test.echo"],
      durationMinutes: 30,
      risk: "low",
      status: "pending",
      createdAt: "2026-05-23T10:00:00Z",
      updatedAt: "2026-05-23T10:00:00Z",
    },
  ],
};

const detailsResponse = {
  ...listResponse.items[0],
  justification: "Need to verify a failing health check.",
  proposedActions: ["tail logs"],
  forbiddenActions: ["restart service"],
  metadata: { ticket: "INC-7" },
};

const approvalResponse = {
  accessRequestId: requestId,
  status: "approved",
  sessionId,
  expiresAt: "2026-05-23T10:30:00Z",
};

const sessionResponse = {
  id: sessionId,
  accessRequestId: requestId,
  status: "active",
  allowedTargets: ["homelab.logs"],
  allowedCapabilities: ["test.echo"],
  createdAt: "2026-05-23T10:00:00Z",
  expiresAt: "2026-05-23T10:30:00Z",
  actionCount: 0,
  maxActionCount: 3,
  completedAt: null,
  revokedAt: null,
  expiredAt: null,
};

const actionResponse = {
  sessionId,
  capability: "test.echo",
  status: "succeeded",
  result: { message: "Hello from approval UI" },
};

const auditResponse = {
  items: [
    {
      id: "33333333-3333-4333-8333-333333333333",
      eventType: "access_request.created",
      aggregateId: requestId,
      occurredAt: "2026-05-23T10:01:00Z",
      details: { requester: "agent-alpha" },
    },
  ],
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

const renderDashboard = (authenticated = true): void => {
  render(
    <TestProviders authenticated={authenticated}>
      <AdminAuthGate>
        <AccessRequestDashboard />
      </AdminAuthGate>
    </TestProviders>,
  );
};

describe("AccessRequestDashboard", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("loads requests and shows human readable details", async (): Promise<void> => {
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo | URL) => {
        const url = input.toString();
        if (url === "/api/v1/access-requests") {
          return Promise.resolve(new Response(JSON.stringify(listResponse)));
        }
        return Promise.resolve(new Response(JSON.stringify(detailsResponse)));
      }),
    );

    renderDashboard();

    expect(await screen.findByText("Inspect service logs")).toBeInTheDocument();
    expect(await screen.findByText("Need to verify a failing health check.")).toBeInTheDocument();
    expect(screen.getByText("tail logs")).toBeInTheDocument();
    expect(screen.getByText("restart service")).toBeInTheDocument();
    expect(screen.getByText("ticket: INC-7")).toBeInTheDocument();
  });

  it("approves a request without an admin token header and can run a dummy action", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      const url = input.toString();
      if (url === "/api/v1/access-requests" && init?.method !== "POST") {
        return Promise.resolve(new Response(JSON.stringify(listResponse)));
      }
      if (url === `/api/v1/access-requests/${requestId}`) {
        return Promise.resolve(new Response(JSON.stringify(detailsResponse)));
      }
      if (url === `/api/v1/access-requests/${requestId}/approve`) {
        return Promise.resolve(new Response(JSON.stringify(approvalResponse)));
      }
      if (url === `/api/v1/sessions/${sessionId}`) {
        return Promise.resolve(new Response(JSON.stringify(sessionResponse)));
      }
      if (url === `/api/v1/sessions/${sessionId}/actions`) {
        return Promise.resolve(new Response(JSON.stringify(actionResponse)));
      }
      return Promise.resolve(new Response(JSON.stringify(listResponse)));
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    renderDashboard();

    await screen.findByText("Inspect service logs");
    localStorage.clear();
    sessionStorage.clear();

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
    await user.type(screen.getByLabelText("Optional decision comment"), "Looks safe");
    await user.click(screen.getByRole("button", { name: "Approve request" }));

    await screen.findByText(new RegExp(sessionId));
    const approveCall = fetchMock.mock.calls.find((call) =>
      call[0].toString().endsWith("/approve"),
    );
    expect(approveCall?.[1]?.headers).not.toMatchObject({
      "X-Gatekeeper-Admin-Token": expect.any(String),
    });

    await screen.findByRole("button", { name: "Run test.echo" });
    await user.click(screen.getByRole("button", { name: "Run test.echo" }));

    await waitFor(() => {
      expect(screen.getByText(/Demo action test.echo succeeded/)).toBeInTheDocument();
    });
  });

  it("renders audit events in context and filters by the selected request id", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      const url = input.toString();
      if (url === "/api/v1/access-requests") {
        return Promise.resolve(new Response(JSON.stringify(listResponse)));
      }
      if (url === `/api/v1/access-requests/${requestId}`) {
        return Promise.resolve(new Response(JSON.stringify(detailsResponse)));
      }
      if (url.startsWith("/api/v1/audit-events")) {
        return Promise.resolve(new Response(JSON.stringify(auditResponse)));
      }
      return Promise.resolve(new Response(JSON.stringify(detailsResponse)));
    });
    vi.stubGlobal("fetch", fetchMock);

    renderDashboard();

    expect(await screen.findByText("Audit events")).toBeInTheDocument();
    await screen.findByText("Inspect service logs");

    expect(await screen.findByText("access_request.created")).toBeInTheDocument();
    expect(screen.getAllByText(requestId).length).toBeGreaterThan(0);

    await waitFor(() => {
      const auditCall = fetchMock.mock.calls.find((call) =>
        call[0].toString().includes(`aggregateId=${requestId}`),
      );
      expect(auditCall?.[0].toString()).toContain(`aggregateId=${requestId}`);
      expect(auditCall?.[1]?.headers).not.toMatchObject({
        "X-Gatekeeper-Admin-Token": expect.any(String),
      });
    });
  });

  it("does not fetch audit events without an authenticated admin session", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      const url = input.toString();
      if (url === "/api/v1/access-requests") {
        return Promise.resolve(new Response(JSON.stringify(listResponse)));
      }
      if (url === `/api/v1/access-requests/${requestId}`) {
        return Promise.resolve(new Response(JSON.stringify(detailsResponse)));
      }
      if (url.startsWith("/api/v1/audit-events")) {
        return Promise.resolve(new Response(JSON.stringify(auditResponse)));
      }
      return Promise.resolve(new Response(JSON.stringify(detailsResponse)));
    });
    vi.stubGlobal("fetch", fetchMock);

    renderDashboard(false);

    expect(await screen.findByText("Lokaler Admin-Login")).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
