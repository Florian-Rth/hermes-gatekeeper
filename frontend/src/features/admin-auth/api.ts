import type { QueryClient } from "@tanstack/react-query";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError, requestJson } from "@/lib/apiClient";
import { adminLoginRequestSchema, adminSessionSchema } from "./schemas";
import type { AdminLoginRequest, AdminSession } from "./types";

export const adminAuthKeys = {
  me: ["admin-auth", "me"] as const,
  sessionExpired: ["admin-auth", "session-expired"] as const,
};

export const unauthenticatedAdminSession: AdminSession = {
  authenticated: false,
  username: "",
};

export const setAdminSessionExpired = (queryClient: QueryClient): void => {
  queryClient.setQueryData(adminAuthKeys.me, unauthenticatedAdminSession);
  queryClient.setQueryData(adminAuthKeys.sessionExpired, true);
};

export const clearAdminSessionExpired = (queryClient: QueryClient): void => {
  queryClient.setQueryData(adminAuthKeys.sessionExpired, false);
};

export const useAdminSessionExpired = () =>
  useQuery<boolean>({
    queryKey: adminAuthKeys.sessionExpired,
    queryFn: () => false,
    initialData: false,
    staleTime: Number.POSITIVE_INFINITY,
  });

export const useAdminSession = () =>
  useQuery<AdminSession>({
    queryKey: adminAuthKeys.me,
    retry: false,
    queryFn: async () => {
      try {
        return await requestJson("/api/v1/admin/me", adminSessionSchema);
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          return unauthenticatedAdminSession;
        }
        throw error;
      }
    },
  });

export const useAdminLogin = () => {
  const queryClient = useQueryClient();
  return useMutation<AdminSession, Error, AdminLoginRequest>({
    mutationFn: async (variables) => {
      const body = adminLoginRequestSchema.parse(variables);
      return requestJson("/api/v1/admin/login", adminSessionSchema, {
        method: "POST",
        body,
      });
    },
    onSuccess: (session) => {
      clearAdminSessionExpired(queryClient);
      queryClient.setQueryData(adminAuthKeys.me, session);
    },
  });
};

export const useAdminLogout = () => {
  const queryClient = useQueryClient();
  return useMutation<AdminSession, Error, void>({
    mutationFn: async () =>
      requestJson("/api/v1/admin/logout", adminSessionSchema, {
        method: "POST",
      }),
    onSuccess: (session) => {
      clearAdminSessionExpired(queryClient);
      queryClient.setQueryData(adminAuthKeys.me, session);
      queryClient.removeQueries({ queryKey: ["audit-events"] });
    },
  });
};
