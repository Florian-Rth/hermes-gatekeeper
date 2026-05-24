import Alert from "@mui/material/Alert";
import type { FC } from "react";
import { useAuditFeedContext } from "./context";

export const ErrorState: FC = () => {
  const { errorMessage, isError } = useAuditFeedContext();

  if (!isError) {
    return null;
  }

  return <Alert severity="error">Audit events could not be loaded. {errorMessage}</Alert>;
};
