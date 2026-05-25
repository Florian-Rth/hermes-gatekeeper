import Alert from "@mui/material/Alert";
import type { FC } from "react";
import { useAuditFeedContext } from "./context";

export const EmptyState: FC = () => {
  const { adminTokenIsMissing, events, hasLoaded, isError, isLoading } = useAuditFeedContext();

  if (adminTokenIsMissing) {
    return <Alert severity="warning">Melde dich als Admin an, um Audit-Events zu laden.</Alert>;
  }

  if (isLoading || isError || !hasLoaded || events.length > 0) {
    return null;
  }

  return <Alert severity="info">No audit events match the current filters.</Alert>;
};
