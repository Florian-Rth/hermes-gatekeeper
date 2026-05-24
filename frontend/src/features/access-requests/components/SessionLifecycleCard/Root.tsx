import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC, ReactNode } from "react";
import { useState } from "react";
import { useAdminToken } from "../../admin-token-context";
import { useCompleteSession, useExecuteDummyAction, useRevokeSession } from "../../api";
import type { SessionActionResult, SessionDetails } from "../../types";
import { SessionLifecycleCardContext } from "./context";

interface RootProps {
  readonly session: SessionDetails | undefined;
  readonly isLoading: boolean;
  readonly children: ReactNode;
}

const getErrorMessage = (error: Error | null): string | null => {
  if (error === null) {
    return null;
  }
  return error.message;
};

const getDummyCapability = (
  session: SessionDetails | undefined,
): "test.echo" | "test.status.read" | null => {
  if (session?.allowedCapabilities.includes("test.echo") === true) {
    return "test.echo";
  }
  if (session?.allowedCapabilities.includes("test.status.read") === true) {
    return "test.status.read";
  }
  return null;
};

const getIsActive = (session: SessionDetails | undefined): boolean => session?.status === "active";

const getIsTerminal = (session: SessionDetails | undefined): boolean =>
  session !== undefined && !getIsActive(session);

const getDummyActionMessage = (result: SessionActionResult | undefined): string | null => {
  if (result === undefined) {
    return null;
  }
  return `Demo action ${result.capability} ${result.status}: ${Object.values(result.result).join(", ")}`;
};

export const Root: FC<RootProps> = ({ session, isLoading, children }) => {
  const { adminToken } = useAdminToken();
  const completeMutation = useCompleteSession();
  const revokeMutation = useRevokeSession();
  const actionMutation = useExecuteDummyAction();
  const [isRevokeDialogOpen, setIsRevokeDialogOpen] = useState(false);
  const isActive = getIsActive(session);
  const isTerminal = getIsTerminal(session);
  const dummyCapability = getDummyCapability(session);
  const adminTokenIsMissing = adminToken.trim() === "";
  const canRevoke = isActive && !adminTokenIsMissing;

  const handleComplete = (): void => {
    if (session === undefined || !isActive) {
      return;
    }
    completeMutation.mutate({ sessionId: session.id });
  };

  const handleRequestRevoke = (): void => {
    if (!canRevoke) {
      return;
    }
    setIsRevokeDialogOpen(true);
  };

  const handleCancelRevoke = (): void => {
    setIsRevokeDialogOpen(false);
  };

  const handleConfirmRevoke = (): void => {
    if (session === undefined || !canRevoke) {
      return;
    }
    revokeMutation.mutate(
      { sessionId: session.id, adminToken: adminToken.trim() },
      { onSuccess: () => setIsRevokeDialogOpen(false) },
    );
  };

  const handleRunDummyAction = (): void => {
    if (session === undefined || !isActive || dummyCapability === null) {
      return;
    }
    actionMutation.mutate({ sessionId: session.id, capability: dummyCapability });
  };

  const lifecycleErrorMessage =
    getErrorMessage(completeMutation.error) ?? getErrorMessage(revokeMutation.error);
  const dummyActionMessage = getDummyActionMessage(actionMutation.data);

  return (
    <SessionLifecycleCardContext.Provider
      value={{
        session,
        isLoading,
        isActive,
        isTerminal,
        dummyCapability,
        canRevoke,
        adminTokenIsMissing,
        isCompleting: completeMutation.isPending,
        isRevoking: revokeMutation.isPending,
        isRunningDummyAction: actionMutation.isPending,
        lifecycleErrorMessage,
        dummyActionMessage,
        isRevokeDialogOpen,
        onComplete: handleComplete,
        onRequestRevoke: handleRequestRevoke,
        onCancelRevoke: handleCancelRevoke,
        onConfirmRevoke: handleConfirmRevoke,
        onRunDummyAction: handleRunDummyAction,
      }}
    >
      <Paper elevation={0} sx={{ border: 1, borderColor: "divider", p: 2 }}>
        <Stack sx={{ gap: 2 }}>
          {isLoading ? <CircularProgress aria-label="Loading session details" size={24} /> : null}
          {!isLoading && session === undefined ? (
            <Typography color="text.secondary">No session selected.</Typography>
          ) : null}
          {session === undefined ? null : children}
          {lifecycleErrorMessage === null ? null : (
            <Alert severity="error">Session lifecycle action failed. {lifecycleErrorMessage}</Alert>
          )}
          {dummyActionMessage === null ? null : (
            <Alert severity="success">{dummyActionMessage}</Alert>
          )}
        </Stack>
      </Paper>
      <Dialog
        aria-labelledby="session-revoke-dialog-title"
        open={isRevokeDialogOpen}
        onClose={handleCancelRevoke}
      >
        <DialogTitle id="session-revoke-dialog-title">Confirm session revocation</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Revoking this session immediately makes it read-only and prevents future demo actions.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleCancelRevoke}>Cancel</Button>
          <Button color="error" disabled={revokeMutation.isPending} onClick={handleConfirmRevoke}>
            {revokeMutation.isPending ? "Revoking..." : "Confirm revoke"}
          </Button>
        </DialogActions>
      </Dialog>
    </SessionLifecycleCardContext.Provider>
  );
};
