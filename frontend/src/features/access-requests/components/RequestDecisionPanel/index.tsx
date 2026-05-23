import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import type { ChangeEvent, FC } from "react";
import { useApproveAccessRequest, useDenyAccessRequest, useExecuteDummyAction } from "../../api";
import type { AccessRequestDetails, ApprovalResult, SessionDetails } from "../../types";

interface RequestDecisionPanelProps {
  readonly request: AccessRequestDetails | undefined;
  readonly adminToken: string;
  readonly comment: string;
  readonly onCommentChange: (value: string) => void;
  readonly approvalResult: ApprovalResult | null;
  readonly session: SessionDetails | undefined;
  readonly isSessionLoading: boolean;
  readonly onApproved: (result: ApprovalResult) => void;
}

const getErrorMessage = (error: Error | null): string | null => {
  if (error === null) {
    return null;
  }
  return error.message;
};

export const RequestDecisionPanel: FC<RequestDecisionPanelProps> = ({
  request,
  adminToken,
  comment,
  onCommentChange,
  approvalResult,
  session,
  isSessionLoading,
  onApproved,
}) => {
  const approveMutation = useApproveAccessRequest();
  const denyMutation = useDenyAccessRequest();
  const actionMutation = useExecuteDummyAction();
  const canDecide =
    request !== undefined && request.status === "pending" && adminToken.trim() !== "";
  const dummyCapability = session?.allowedCapabilities.includes("test.echo")
    ? "test.echo"
    : session?.allowedCapabilities.includes("test.status.read")
      ? "test.status.read"
      : null;

  const handleCommentChange = (event: ChangeEvent<HTMLInputElement>): void => {
    onCommentChange(event.target.value);
  };

  const handleApprove = (): void => {
    if (request === undefined) {
      return;
    }
    approveMutation.mutate(
      { id: request.id, adminToken, comment },
      { onSuccess: (result) => onApproved(result) },
    );
  };

  const handleDeny = (): void => {
    if (request === undefined) {
      return;
    }
    denyMutation.mutate({ id: request.id, adminToken, comment });
  };

  const handleDummyAction = (): void => {
    if (session === undefined || dummyCapability === null) {
      return;
    }
    actionMutation.mutate({ sessionId: session.id, capability: dummyCapability });
  };

  const errorMessage =
    getErrorMessage(approveMutation.error) ??
    getErrorMessage(denyMutation.error) ??
    getErrorMessage(actionMutation.error);

  return (
    <Paper elevation={1} sx={{ p: 3 }}>
      <Stack spacing={2}>
        <Typography component="h2" variant="h5">
          Decision
        </Typography>
        {request === undefined ? (
          <Typography color="text.secondary">
            Select a pending request to approve or deny it.
          </Typography>
        ) : null}
        {request !== undefined && request.status !== "pending" ? (
          <Alert severity="info">This request is already {request.status.toLowerCase()}.</Alert>
        ) : null}
        {request !== undefined && request.status === "pending" && adminToken.trim() === "" ? (
          <Alert severity="warning">Enter the admin token before approving or denying.</Alert>
        ) : null}
        <TextField
          fullWidth
          label="Optional decision comment"
          multiline
          minRows={3}
          value={comment}
          onChange={handleCommentChange}
        />
        {errorMessage === null ? null : (
          <Alert severity="error">
            Action failed. Check the admin token or request status. {errorMessage}
          </Alert>
        )}
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
          <Button
            disabled={!canDecide || approveMutation.isPending}
            onClick={handleApprove}
            variant="contained"
          >
            {approveMutation.isPending ? "Approving..." : "Approve request"}
          </Button>
          <Button
            color="error"
            disabled={!canDecide || denyMutation.isPending}
            onClick={handleDeny}
            variant="contained"
          >
            {denyMutation.isPending ? "Denying..." : "Deny request"}
          </Button>
        </Stack>
        {approvalResult === null ? null : (
          <Alert severity="success">
            Approved. Session {approvalResult.sessionId} expires at {approvalResult.expiresAt}.
          </Alert>
        )}
        {isSessionLoading ? (
          <CircularProgress aria-label="Loading session details" size={24} />
        ) : null}
        {session === undefined ? null : (
          <Stack spacing={1}>
            <Typography component="h3" variant="h6">
              Session details
            </Typography>
            <Typography>Status: {session.status}</Typography>
            <Typography>Expires: {session.expiresAt}</Typography>
            <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
              {session.allowedCapabilities.map((capability) => (
                <Chip key={capability} label={capability} size="small" />
              ))}
            </Stack>
            {dummyCapability === null ? null : (
              <Button
                disabled={actionMutation.isPending}
                onClick={handleDummyAction}
                variant="outlined"
              >
                {actionMutation.isPending ? "Running demo action..." : `Run ${dummyCapability}`}
              </Button>
            )}
          </Stack>
        )}
        {actionMutation.data === undefined ? null : (
          <Alert severity="success">
            Demo action {actionMutation.data.capability} {actionMutation.data.status}:{" "}
            {Object.values(actionMutation.data.result).join(", ")}
          </Alert>
        )}
      </Stack>
    </Paper>
  );
};
