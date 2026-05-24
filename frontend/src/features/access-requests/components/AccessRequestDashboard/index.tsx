import type { FC } from "react";
import { AdminTokenProvider } from "../../admin-token-context";
import { AccessRequestDashboardContent } from "./AccessRequestDashboardContent";

export const AccessRequestDashboard: FC = () => (
  <AdminTokenProvider>
    <AccessRequestDashboardContent />
  </AdminTokenProvider>
);
