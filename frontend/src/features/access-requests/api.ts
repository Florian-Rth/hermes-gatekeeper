import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { requestJson } from "@/lib/apiClient";
import {
  accessRequestDetailsSchema,
  approvalResultSchema,
  denialResultSchema,
  listAccessRequestsResponseSchema,
  sessionActionResultSchema,
  sessionDetailsSchema,
} from "./schemas";
import type {
  AccessRequestDetails,
  AccessRequestSummary,
  ApprovalResult,
  DenialResult,
  SessionActionResult,
  SessionDetails,
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

const accessRequestKeys = {
  all: ["access-requests"] as const,
  detail: (id: string) => ["access-requests", id] as const,
  session: (id: string) => ["sessions", id] as const,
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
      ]);
    },
  });
};

export const useSessionDetails = (id: string | null) =>
  useQuery<SessionDetails>({
    queryKey: id === null ? ["sessions", "none"] : accessRequestKeys.session(id),
    enabled: id !== null,
    queryFn: async () => requestJson(`/api/v1/sessions/${id}`, sessionDetailsSchema),
  });

export const useExecuteDummyAction = () =>
  useMutation<SessionActionResult, Error, ExecuteDummyActionVariables>({
    mutationFn: async ({ sessionId, capability }) =>
      requestJson(`/api/v1/sessions/${sessionId}/actions`, sessionActionResultSchema, {
        method: "POST",
        body: {
          capability,
          payload: capability === "test.echo" ? { message: "Hello from approval UI" } : {},
        },
      }),
  });
