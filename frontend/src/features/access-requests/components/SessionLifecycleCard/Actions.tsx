import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import type { FC } from "react";
import { useSessionLifecycleCardContext } from "./context";

export const Actions: FC = () => {
  const {
    session,
    isActive,
    isTerminal,
    dummyCapability,
    canRevoke,
    isCompleting,
    isRevoking,
    isRunningDummyAction,
    onComplete,
    onRequestRevoke,
    onRunDummyAction,
  } = useSessionLifecycleCardContext();

  if (session === undefined) {
    return null;
  }

  return (
    <Stack sx={{ gap: 1.5 }}>
      {isTerminal ? <Alert severity="info">This session is terminal and read-only.</Alert> : null}
      <Stack direction={{ xs: "column", sm: "row" }} sx={{ gap: 1 }}>
        <Button disabled={!isActive || isCompleting} onClick={onComplete} variant="contained">
          {isCompleting ? "Completing..." : "Complete session"}
        </Button>
        <Button
          color="error"
          disabled={!canRevoke || isRevoking}
          onClick={onRequestRevoke}
          variant="contained"
        >
          {isRevoking ? "Revoking..." : "Revoke session"}
        </Button>
        {dummyCapability === null ? null : (
          <Button
            disabled={!isActive || isRunningDummyAction}
            onClick={onRunDummyAction}
            variant="outlined"
          >
            {isRunningDummyAction ? "Running demo action..." : `Run ${dummyCapability}`}
          </Button>
        )}
      </Stack>
    </Stack>
  );
};
