import type { FC } from "react";
import { AccessRequestDashboard } from "@/features/access-requests";
import { AdminAuthGate } from "@/features/admin-auth";

export const HomePage: FC = () => (
  <AdminAuthGate>
    <AccessRequestDashboard />
  </AdminAuthGate>
);
