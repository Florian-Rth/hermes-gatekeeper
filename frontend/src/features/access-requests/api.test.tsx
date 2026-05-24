import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, waitFor } from "@testing-library/react";
import type { FC, ReactNode } from "react";
import { useEffect } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  useApproveAccessRequest,
  useAuditEvents,
  useDenyAccessRequest,
  useExecuteDummyAction,
  useRevokeSession,
} from "./api";
import { sessionDetailsSchema } from "./schemas";

const requestId = "11111111-1111-4111-8111-111111111111";
const sessionId = "22222222-2222-4222-8222-222222222222";
const auditEventId = "33333333-3333-4333-8333-333333333333";

const completedSessionResponse = {
  id: sessionId,
  accessRequestId: requestId,
  status: "completed",
  allowedTargets: ["homelab.logs"],
  allowedCapabilities: ["test.echo"],
  createdAt: "2026-05-23T10:00:00Z",
  expiresAt: "2026-05-23T10:30:00Z",
  actionCount: 1,
  maxActionCount: 3,
  completedAt: "2026-05-23T10:10:00Z",
  revokedAt: null,
  expiredAt: null,
};

const revokedSessionResponse = {
  id: sessionId,
  accessRequestId: requestId,
  status: "revoked",
  createdAt: "2026-05-23T10:00:00Z",
  expiresAt: "2026-05-23T10:30:00Z",
  completedAt: null,
  revokedAt: "2026-05-23T10:12:00Z",
  expiredAt: null,
};

const approvalResponse = {
  accessRequestId: requestId,
  status: "approved",
  sessionId,
  expiresAt: "2026-05-23T10:30:00Z",
};

const denialResponse = {
  accessRequestId: requestId,
  status: "denied",
};

const dummyActionResponse = {
  sessionId,
  capability: "test.echo",
  status: "ok",
  result: { message: "Hello from approval UI" },
};

interface TestProvidersProps {
  readonly children: ReactNode;
  readonly queryClient?: QueryClient;
}

const TestProviders: FC<TestProvidersProps> = ({ children, queryClient }) => {
  const client =
    queryClient ??
    new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
};

describe("access request api boundary", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("accepts session lifecycle fields and action budgets", (): void => {
    expect(sessionDetailsSchema.parse(completedSessionResponse)).toMatchObject({
      status: "completed",
      actionCount: 1,
      maxActionCount: 3,
      completedAt: "2026-05-23T10:10:00Z",
    });

    expect(
      sessionDetailsSchema.parse({
        ...completedSessionResponse,
        status: "expired",
        completedAt: null,
        expiredAt: "2026-05-23T10:31:00Z",
      }),
    ).toMatchObject({ status: "expired" });

    expect(
      sessionDetailsSchema.parse({
        ...completedSessionResponse,
        status: "revoked",
        completedAt: null,
        revokedAt: "2026-05-23T10:12:00Z",
      }),
    ).toMatchObject({ status: "revoked" });
  });

  it("revokes a session with the admin token header", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      if (input.toString() === `/api/v1/sessions/${sessionId}/revoke`) {
        return Promise.resolve(new Response(JSON.stringify(revokedSessionResponse)));
      }
      return Promise.resolve(new Response(JSON.stringify({ items: [] })));
    });
    vi.stubGlobal("fetch", fetchMock);

    const RevokeHarness: FC = () => {
      const mutation = useRevokeSession();
      useEffect(() => {
        mutation.mutate({ sessionId, adminToken: "secret-token" });
      }, [mutation]);
      return null;
    };

    render(
      <TestProviders>
        <RevokeHarness />
      </TestProviders>,
    );

    await waitFor(() => {
      const revokeCall = fetchMock.mock.calls.find((call) =>
        call[0].toString().endsWith("/revoke"),
      );
      expect(revokeCall?.[1]?.headers).toMatchObject({
        "X-Gatekeeper-Admin-Token": "secret-token",
      });
    });
  });

  it("invalidates access request and audit event queries after decisions", async (): Promise<void> => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    const fetchMock = vi.fn((input: RequestInfo | URL, _init?: RequestInit) => {
      if (input.toString().endsWith("/approve")) {
        return Promise.resolve(new Response(JSON.stringify(approvalResponse)));
      }
      if (input.toString().endsWith("/deny")) {
        return Promise.resolve(new Response(JSON.stringify(denialResponse)));
      }
      return Promise.resolve(new Response(JSON.stringify({ items: [] })));
    });
    vi.stubGlobal("fetch", fetchMock);

    const DecisionHarness: FC = () => {
      const approveMutation = useApproveAccessRequest();
      const denyMutation = useDenyAccessRequest();
      useEffect(() => {
        approveMutation.mutate({ id: requestId, adminToken: "secret-token", comment: "ok" });
        denyMutation.mutate({ id: requestId, adminToken: "secret-token", comment: "no" });
      }, [approveMutation, denyMutation]);
      return null;
    };

    render(
      <TestProviders queryClient={queryClient}>
        <DecisionHarness />
      </TestProviders>,
    );

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["audit-events"] });
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["access-requests"] });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["access-requests", requestId] });
  });

  it("invalidates session detail and audit event queries after dummy actions", async (): Promise<void> => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const invalidateSpy = vi.spyOn(queryClient, "invalidateQueries");
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
      Promise.resolve(new Response(JSON.stringify(dummyActionResponse))),
    );
    vi.stubGlobal("fetch", fetchMock);

    const ActionHarness: FC = () => {
      const mutation = useExecuteDummyAction();
      useEffect(() => {
        mutation.mutate({ sessionId, capability: "test.echo" });
      }, [mutation]);
      return null;
    };

    render(
      <TestProviders queryClient={queryClient}>
        <ActionHarness />
      </TestProviders>,
    );

    await waitFor(() => {
      expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["sessions", sessionId] });
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["audit-events"] });
  });

  it("loads audit events with encoded filters and admin token", async (): Promise<void> => {
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            items: [
              {
                id: auditEventId,
                eventType: "SessionRevoked",
                aggregateId: sessionId,
                occurredAt: "2026-05-23T10:12:00Z",
                details: { reason: "manual revocation" },
              },
            ],
            nextCursor: "next-page",
          }),
        ),
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const AuditHarness: FC = () => {
      useAuditEvents(
        {
          aggregateId: sessionId,
          eventType: "Session Revoked",
          from: "2026-05-23T10:00:00Z",
          to: "2026-05-23T11:00:00Z",
          cursor: "cursor+one",
          limit: 25,
        },
        "secret-token",
        1,
      );
      return null;
    };

    render(
      <TestProviders>
        <AuditHarness />
      </TestProviders>,
    );

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalled();
    });

    const [input, init] = fetchMock.mock.calls[0];
    expect(input.toString()).toBe(
      `/api/v1/audit-events?aggregateId=${sessionId}&eventType=Session+Revoked&from=2026-05-23T10%3A00%3A00Z&to=2026-05-23T11%3A00%3A00Z&cursor=cursor%2Bone&limit=25`,
    );
    expect(init?.headers).toMatchObject({
      "X-Gatekeeper-Admin-Token": "secret-token",
    });
  });

  it("does not load audit events until an admin token is present", (): void => {
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
      Promise.resolve(new Response(JSON.stringify({ items: [], nextCursor: null }))),
    );
    vi.stubGlobal("fetch", fetchMock);

    const AuditHarness: FC = () => {
      useAuditEvents({ aggregateId: sessionId }, "   ", 0);
      return null;
    };

    render(
      <TestProviders>
        <AuditHarness />
      </TestProviders>,
    );

    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("refetches audit events when the admin token changes without storing the token in the query key", async (): Promise<void> => {
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
      Promise.resolve(new Response(JSON.stringify({ items: [], nextCursor: null }))),
    );
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    vi.stubGlobal("fetch", fetchMock);

    interface AuditHarnessProps {
      readonly adminToken: string;
      readonly adminTokenVersion: number;
    }

    const AuditHarness: FC<AuditHarnessProps> = ({ adminToken, adminTokenVersion }) => {
      useAuditEvents({ aggregateId: sessionId }, adminToken, adminTokenVersion);
      return null;
    };

    const { rerender } = render(
      <TestProviders queryClient={queryClient}>
        <AuditHarness adminToken="first-token" adminTokenVersion={1} />
      </TestProviders>,
    );

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    rerender(
      <TestProviders queryClient={queryClient}>
        <AuditHarness adminToken="second-token" adminTokenVersion={2} />
      </TestProviders>,
    );

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });
    expect(fetchMock.mock.calls[0][1]?.headers).toMatchObject({
      "X-Gatekeeper-Admin-Token": "first-token",
    });
    expect(fetchMock.mock.calls[1][1]?.headers).toMatchObject({
      "X-Gatekeeper-Admin-Token": "second-token",
    });

    const queryKeys = queryClient
      .getQueryCache()
      .getAll()
      .map((query) => query.queryKey);
    const serializedQueryKeys = JSON.stringify(queryKeys);
    expect(serializedQueryKeys).not.toContain("first-token");
    expect(serializedQueryKeys).not.toContain("second-token");
  });

  it("uses distinct audit event query keys for token versions across remounts", async (): Promise<void> => {
    const fetchMock = vi.fn((_input: RequestInfo | URL, _init?: RequestInit) =>
      Promise.resolve(new Response(JSON.stringify({ items: [], nextCursor: null }))),
    );
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
        mutations: { retry: false },
      },
    });
    vi.stubGlobal("fetch", fetchMock);

    interface AuditHarnessProps {
      readonly adminToken: string;
      readonly adminTokenVersion: number;
    }

    const filters = { aggregateId: sessionId };

    const AuditHarness: FC<AuditHarnessProps> = ({ adminToken, adminTokenVersion }) => {
      useAuditEvents(filters, adminToken, adminTokenVersion);
      return null;
    };

    const firstRender = render(
      <TestProviders queryClient={queryClient}>
        <AuditHarness adminToken="first-initial-token" adminTokenVersion={1} />
      </TestProviders>,
    );

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(1);
    });

    firstRender.unmount();

    render(
      <TestProviders queryClient={queryClient}>
        <AuditHarness adminToken="second-initial-token" adminTokenVersion={2} />
      </TestProviders>,
    );

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });

    expect(fetchMock.mock.calls[0][1]?.headers).toMatchObject({
      "X-Gatekeeper-Admin-Token": "first-initial-token",
    });
    expect(fetchMock.mock.calls[1][1]?.headers).toMatchObject({
      "X-Gatekeeper-Admin-Token": "second-initial-token",
    });

    const auditQueryKeys = queryClient
      .getQueryCache()
      .getAll()
      .map((query) => query.queryKey)
      .filter((queryKey) => queryKey[0] === "audit-events");
    expect(auditQueryKeys).toHaveLength(2);
    expect(new Set(auditQueryKeys.map((queryKey) => JSON.stringify(queryKey))).size).toBe(2);

    const serializedAuditQueryKeys = JSON.stringify(auditQueryKeys);
    expect(serializedAuditQueryKeys).not.toContain("first-initial-token");
    expect(serializedAuditQueryKeys).not.toContain("second-initial-token");
  });
});
