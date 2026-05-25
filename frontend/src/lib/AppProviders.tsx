import CssBaseline from "@mui/material/CssBaseline";
import ThemeProvider from "@mui/material/styles/ThemeProvider";
import { QueryClientProvider } from "@tanstack/react-query";
import type { FC, ReactNode } from "react";
import { AdminAuthProvider } from "@/features/admin-auth";
import { queryClient } from "@/lib/queryClient";
import { theme } from "@/styles/theme";

interface AppProvidersProps {
  readonly children: ReactNode;
}

export const AppProviders: FC<AppProvidersProps> = ({ children }) => (
  <QueryClientProvider client={queryClient}>
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <AdminAuthProvider>{children}</AdminAuthProvider>
    </ThemeProvider>
  </QueryClientProvider>
);
