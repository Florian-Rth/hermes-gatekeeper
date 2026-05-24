import Chip from "@mui/material/Chip";
import type { FC } from "react";
import { useSessionLifecycleCardContext } from "./context";

const getStatusColor = (status: string): "success" | "info" | "warning" | "default" => {
  if (status === "active") {
    return "success";
  }
  if (status === "completed") {
    return "info";
  }
  if (status === "expired") {
    return "warning";
  }
  return "default";
};

export const StatusBadge: FC = () => {
  const { session } = useSessionLifecycleCardContext();

  if (session === undefined) {
    return null;
  }

  return <Chip color={getStatusColor(session.status)} label={session.status} size="small" />;
};
