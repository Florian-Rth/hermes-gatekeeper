import type { FC, ReactNode } from "react";
import { useAdminAuth } from "../../admin-auth-context";
import { AdminLoginPanel } from "../AdminLoginPanel";

interface AdminAuthGateProps {
  readonly children: ReactNode;
}

export const AdminAuthGate: FC<AdminAuthGateProps> = ({ children }) => {
  const { session } = useAdminAuth();

  if (!session.authenticated) {
    return <AdminLoginPanel />;
  }

  return <>{children}</>;
};
