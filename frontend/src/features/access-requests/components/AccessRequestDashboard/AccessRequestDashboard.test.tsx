import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { FC, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
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
};

const actionResponse = {
  sessionId,
  capability: "test.echo",
  status: "succeeded",
  result: { message: "Hello from approval UI" },
};

interface TestProvidersProps {
  readonly children: ReactNode;
}

const TestProviders: FC<TestProvidersProps> = ({ children }) => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
};

const renderDashboard = (): void => {
  render(
    <TestProviders>
      <AccessRequestDashboard />
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

  it("approves a request with the admin token header and can run a dummy action", async (): Promise<void> => {
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
    await user.type(screen.getByLabelText("X-Gatekeeper-Admin-Token"), "secret-token");
    await user.type(screen.getByLabelText("Optional decision comment"), "Looks safe");
    await user.click(screen.getByRole("button", { name: "Approve request" }));

    await screen.findByText(new RegExp(sessionId));
    const approveCall = fetchMock.mock.calls.find((call) =>
      call[0].toString().endsWith("/approve"),
    );
    expect(approveCall?.[1]?.headers).toMatchObject({
      "X-Gatekeeper-Admin-Token": "secret-token",
    });

    await screen.findByRole("button", { name: "Run test.echo" });
    await user.click(screen.getByRole("button", { name: "Run test.echo" }));

    await waitFor(() => {
      expect(screen.getByText(/Demo action test.echo succeeded/)).toBeInTheDocument();
    });
  });
});
