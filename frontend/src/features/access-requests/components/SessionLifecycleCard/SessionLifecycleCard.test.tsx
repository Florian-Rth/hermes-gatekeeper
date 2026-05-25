import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { FC, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { SessionDetails } from "../../types";
import { SessionLifecycleCard } from ".";

const activeSession: SessionDetails = {
  id: "22222222-2222-4222-8222-222222222222",
  accessRequestId: "11111111-1111-4111-8111-111111111111",
  status: "active",
  allowedTargets: ["homelab.logs", "homelab.metrics"],
  allowedCapabilities: ["test.echo", "test.status.read"],
  createdAt: "2026-05-23T10:00:00Z",
  expiresAt: "2026-05-23T10:30:00Z",
  actionCount: 1,
  maxActionCount: 3,
  completedAt: null,
  revokedAt: null,
  expiredAt: null,
};

const terminalSession: SessionDetails = {
  ...activeSession,
  status: "completed",
  completedAt: "2026-05-23T10:12:00Z",
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

const renderCard = (session: SessionDetails | undefined = activeSession): void => {
  render(
    <TestProviders>
      <SessionLifecycleCard.Root session={session} isLoading={false}>
        <SessionLifecycleCard.Header />
        <SessionLifecycleCard.StatusBadge />
        <SessionLifecycleCard.Budget />
        <SessionLifecycleCard.Capabilities />
        <SessionLifecycleCard.Timestamps />
        <SessionLifecycleCard.Actions />
      </SessionLifecycleCard.Root>
    </TestProviders>,
  );
};

describe("SessionLifecycleCard", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("shows status, budget, capabilities, targets, and timestamps for an active session", (): void => {
    renderCard();

    expect(screen.getByText(/Session 22222222-2222-4222-8222-222222222222/)).toBeInTheDocument();
    expect(
      screen.getByText(/Access request 11111111-1111-4111-8111-111111111111/),
    ).toBeInTheDocument();
    expect(screen.getByText("active")).toBeInTheDocument();
    expect(screen.getByText("Actions used: 1 / 3")).toBeInTheDocument();
    expect(screen.getByText("homelab.logs")).toBeInTheDocument();
    expect(screen.getByText("homelab.metrics")).toBeInTheDocument();
    expect(screen.getByText("test.echo")).toBeInTheDocument();
    expect(screen.getByText("test.status.read")).toBeInTheDocument();
    expect(screen.getByText(/Created: 2026-05-23T10:00:00Z/)).toBeInTheDocument();
    expect(screen.getByText(/Expires: 2026-05-23T10:30:00Z/)).toBeInTheDocument();
  });

  it("sends complete session requests", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      if (input.toString() === `/api/v1/sessions/${activeSession.id}/complete`) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: activeSession.id,
              accessRequestId: activeSession.accessRequestId,
              status: "completed",
              createdAt: activeSession.createdAt,
              expiresAt: activeSession.expiresAt,
              completedAt: "2026-05-23T10:14:00Z",
              revokedAt: null,
              expiredAt: null,
            }),
          ),
        );
      }
      return Promise.resolve(new Response(JSON.stringify(activeSession)));
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    renderCard();
    await user.click(screen.getByRole("button", { name: "Complete session" }));

    await waitFor(() => {
      const completeCall = fetchMock.mock.calls.find((call) =>
        call[0].toString().endsWith("/complete"),
      );
      expect(completeCall?.[1]?.method).toBe("POST");
    });
  });

  it("requires confirmation and sends revoke requests without an admin token header", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      if (input.toString() === `/api/v1/sessions/${activeSession.id}/revoke`) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: activeSession.id,
              accessRequestId: activeSession.accessRequestId,
              status: "revoked",
              createdAt: activeSession.createdAt,
              expiresAt: activeSession.expiresAt,
              completedAt: null,
              revokedAt: "2026-05-23T10:15:00Z",
              expiredAt: null,
            }),
          ),
        );
      }
      return Promise.resolve(new Response(JSON.stringify(activeSession)));
    });
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    renderCard();
    await user.click(screen.getByRole("button", { name: "Revoke session" }));
    expect(screen.getByRole("dialog", { name: "Confirm session revocation" })).toBeInTheDocument();
    expect(fetchMock).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Confirm revoke" }));

    await waitFor(() => {
      const revokeCall = fetchMock.mock.calls.find((call) =>
        call[0].toString().endsWith("/revoke"),
      );
      expect(revokeCall?.[1]?.method).toBe("POST");
      expect(revokeCall?.[1]?.headers).not.toMatchObject({
        "X-Gatekeeper-Admin-Token": expect.any(String),
      });
    });
  });

  it("disables invalid lifecycle and demo actions for terminal sessions", (): void => {
    renderCard(terminalSession);

    expect(screen.getByText("This session is terminal and read-only."));
    expect(screen.getByRole("button", { name: "Complete session" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Revoke session" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Run test.echo" })).toBeDisabled();
    expect(screen.getByText(/Completed: 2026-05-23T10:12:00Z/)).toBeInTheDocument();
  });
});
