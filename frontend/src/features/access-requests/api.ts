import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { requestJson } from "@/lib/apiClient";
import {
  accessRequestDetailsSchema,
  approvalResultSchema,
  denialResultSchema,
  listAccessRequestsResponseSchema,
  listAuditEventsResponseSchema,
  sessionActionResultSchema,
  sessionDetailsSchema,
  sessionLifecycleResponseSchema,
} from "./schemas";
import type {
  AccessRequestDetails,
  AccessRequestSummary,
  ApprovalResult,
  AuditEventFilters,
  DenialResult,
  ListAuditEventsResponse,
  SessionActionResult,
  SessionDetails,
  SessionLifecycleResponse,
} from "./types";

interface DecisionVariables {
  readonly id: string;
  readonly adminToken: string;
  readonly comment: string;
}

interface ExecuteDummyActionVariables {
  readonly sessionId: string;
  readonly capability: "test.echo" | "test.status.read";
}

interface SessionLifecycleVariables {
  readonly sessionId: string;
}

interface RevokeSessionVariables extends SessionLifecycleVariables {
  readonly adminToken: string;
}

const accessRequestKeys = {
  all: ["access-requests"] as const,
  detail: (id: string) => ["access-requests", id] as const,
};

const sessionKeys = {
  all: ["sessions"] as const,
  detail: (id: string) => ["sessions", id] as const,
};

const auditEventKeys = {
  all: ["audit-events"] as const,
  list: (filters: AuditEventFilters, adminTokenVersion: number) =>
    ["audit-events", filters, adminTokenVersion] as const,
};

const appendFilter = (
  searchParams: URLSearchParams,
  key: string,
  value: string | number | undefined,
): void => {
  if (value !== undefined) {
    searchParams.set(key, value.toString());
  }
};

const buildAuditEventsPath = (filters: AuditEventFilters): string => {
  const searchParams = new URLSearchParams();
  appendFilter(searchParams, "aggregateId", filters.aggregateId);
  appendFilter(searchParams, "eventType", filters.eventType);
  appendFilter(searchParams, "from", filters.from);
  appendFilter(searchParams, "to", filters.to);
  appendFilter(searchParams, "cursor", filters.cursor);
  appendFilter(searchParams, "limit", filters.limit);
  const queryString = searchParams.toString();
  return queryString.length === 0 ? "/api/v1/audit-events" : `/api/v1/audit-events?${queryString}`;
};

export const useAccessRequests = () =>
  useQuery<ReadonlyArray<AccessRequestSummary>>({
    queryKey: accessRequestKeys.all,
    queryFn: async () => {
      const response = await requestJson(
        "/api/v1/access-requests",
        listAccessRequestsResponseSchema,
      );
      return response.items;
    },
  });

export const useAccessRequestDetails = (id: string | null) =>
  useQuery<AccessRequestDetails>({
    queryKey: id === null ? ["access-requests", "none"] : accessRequestKeys.detail(id),
    enabled: id !== null,
    queryFn: async () => requestJson(`/api/v1/access-requests/${id}`, accessRequestDetailsSchema),
  });

export const useApproveAccessRequest = () => {
  const queryClient = useQueryClient();
  return useMutation<ApprovalResult, Error, DecisionVariables>({
    mutationFn: async ({ id, adminToken, comment }) =>
      requestJson(`/api/v1/access-requests/${id}/approve`, approvalResultSchema, {
        method: "POST",
        headers: { "X-Gatekeeper-Admin-Token": adminToken },
        body: { comment },
      }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: accessRequestKeys.all }),
        queryClient.invalidateQueries({ queryKey: accessRequestKeys.detail(variables.id) }),
        queryClient.invalidateQueries({ queryKey: auditEventKeys.all }),
      ]);
    },
  });
};

export const useDenyAccessRequest = () => {
  const queryClient = useQueryClient();
  return useMutation<DenialResult, Error, DecisionVariables>({
    mutationFn: async ({ id, adminToken, comment }) =>
      requestJson(`/api/v1/access-requests/${id}/deny`, denialResultSchema, {
        method: "POST",
        headers: { "X-Gatekeeper-Admin-Token": adminToken },
        body: { comment },
      }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: accessRequestKeys.all }),
        queryClient.invalidateQueries({ queryKey: accessRequestKeys.detail(variables.id) }),
        queryClient.invalidateQueries({ queryKey: auditEventKeys.all }),
      ]);
    },
  });
};

export const useSessionDetails = (id: string | null) =>
  useQuery<SessionDetails>({
    queryKey: id === null ? ["sessions", "none"] : sessionKeys.detail(id),
    enabled: id !== null,
    queryFn: async () => requestJson(`/api/v1/sessions/${id}`, sessionDetailsSchema),
  });

export const useCompleteSession = () => {
  const queryClient = useQueryClient();
  return useMutation<SessionLifecycleResponse, Error, SessionLifecycleVariables>({
    mutationFn: async ({ sessionId }) =>
      requestJson(`/api/v1/sessions/${sessionId}/complete`, sessionLifecycleResponseSchema, {
        method: "POST",
      }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: sessionKeys.detail(variables.sessionId) }),
        queryClient.invalidateQueries({ queryKey: auditEventKeys.all }),
      ]);
    },
  });
};

export const useRevokeSession = () => {
  const queryClient = useQueryClient();
  return useMutation<SessionLifecycleResponse, Error, RevokeSessionVariables>({
    mutationFn: async ({ sessionId, adminToken }) =>
      requestJson(`/api/v1/sessions/${sessionId}/revoke`, sessionLifecycleResponseSchema, {
        method: "POST",
        headers: { "X-Gatekeeper-Admin-Token": adminToken },
      }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: sessionKeys.detail(variables.sessionId) }),
        queryClient.invalidateQueries({ queryKey: auditEventKeys.all }),
      ]);
    },
  });
};

export const useAuditEvents = (
  filters: AuditEventFilters,
  adminToken: string,
  adminTokenVersion: number,
) => {
  const trimmedAdminToken = adminToken.trim();
  return useQuery<ListAuditEventsResponse>({
    queryKey: auditEventKeys.list(filters, adminTokenVersion),
    enabled: trimmedAdminToken.length > 0,
    queryFn: async () =>
      requestJson(buildAuditEventsPath(filters), listAuditEventsResponseSchema, {
        headers: { "X-Gatekeeper-Admin-Token": trimmedAdminToken },
      }),
  });
};

export const useExecuteDummyAction = () => {
  const queryClient = useQueryClient();
  return useMutation<SessionActionResult, Error, ExecuteDummyActionVariables>({
    mutationFn: async ({ sessionId, capability }) =>
      requestJson(`/api/v1/sessions/${sessionId}/actions`, sessionActionResultSchema, {
        method: "POST",
        body: {
          capability,
          payload: capability === "test.echo" ? { message: "Hello from approval UI" } : {},
        },
      }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: sessionKeys.detail(variables.sessionId) }),
        queryClient.invalidateQueries({ queryKey: auditEventKeys.all }),
      ]);
    },
  });
};
