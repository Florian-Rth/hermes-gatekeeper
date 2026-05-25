import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { useQueryClient } from "@tanstack/react-query";
import type { FC, ReactNode } from "react";
import { createContext, useContext } from "react";
import {
  clearAdminSessionExpired,
  setAdminSessionExpired,
  useAdminSession,
  useAdminSessionExpired,
} from "./api";
import type { AdminSession } from "./types";

interface AdminAuthContextValue {
  readonly session: AdminSession;
  readonly isLoading: boolean;
  readonly sessionExpiredMessage: string | null;
  readonly clearSessionExpiredMessage: () => void;
  readonly markSessionExpired: () => void;
}

interface AdminAuthProviderProps {
  readonly children: ReactNode;
}

const AdminAuthContext = createContext<AdminAuthContextValue | null>(null);

export const AdminAuthProvider: FC<AdminAuthProviderProps> = ({ children }) => {
  const queryClient = useQueryClient();
  const sessionQuery = useAdminSession();
  const sessionExpiredQuery = useAdminSessionExpired();
  const session = sessionQuery.data ?? { authenticated: false, username: "" };
  const sessionExpiredMessage = sessionExpiredQuery.data
    ? "Deine Admin-Sitzung ist abgelaufen. Bitte melde dich erneut an."
    : null;

  const clearSessionExpiredMessage = (): void => {
    clearAdminSessionExpired(queryClient);
  };

  const markSessionExpired = (): void => {
    setAdminSessionExpired(queryClient);
  };

  if (sessionQuery.isLoading) {
    return (
      <Stack sx={{ alignItems: "center", justifyContent: "center", minHeight: "100vh", gap: 2 }}>
        <CircularProgress aria-label="Admin session wird geprüft" />
        <Typography color="text.secondary">Admin-Sitzung wird geprüft…</Typography>
      </Stack>
    );
  }

  return (
    <AdminAuthContext.Provider
      value={{
        session,
        isLoading: sessionQuery.isLoading,
        sessionExpiredMessage,
        clearSessionExpiredMessage,
        markSessionExpired,
      }}
    >
      {children}
    </AdminAuthContext.Provider>
  );
};

export const useAdminAuth = (): AdminAuthContextValue => {
  const context = useContext(AdminAuthContext);

  if (context === null) {
    throw new Error("useAdminAuth must be used within AdminAuthProvider");
  }

  return context;
};
