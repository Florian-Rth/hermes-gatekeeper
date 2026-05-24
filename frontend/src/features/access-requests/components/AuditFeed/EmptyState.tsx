import Alert from "@mui/material/Alert";
import type { FC } from "react";
import { useAuditFeedContext } from "./context";

export const EmptyState: FC = () => {
  const { adminTokenIsMissing, events, hasLoaded, isError, isLoading } = useAuditFeedContext();

  if (adminTokenIsMissing) {
    return <Alert severity="warning">Enter the admin token to load audit events.</Alert>;
  }

  if (isLoading || isError || !hasLoaded || events.length > 0) {
    return null;
  }

  return <Alert severity="info">No audit events match the current filters.</Alert>;
};
