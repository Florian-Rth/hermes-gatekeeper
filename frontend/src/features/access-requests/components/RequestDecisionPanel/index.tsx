import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import type { ChangeEvent, FC } from "react";
import { useApproveAccessRequest, useDenyAccessRequest } from "../../api";
import type { AccessRequestDetails, ApprovalResult, SessionDetails } from "../../types";
import { SessionLifecycleCard } from "../SessionLifecycleCard";

interface RequestDecisionPanelProps {
  readonly request: AccessRequestDetails | undefined;
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
  comment,
  onCommentChange,
  approvalResult,
  session,
  isSessionLoading,
  onApproved,
}) => {
  const approveMutation = useApproveAccessRequest();
  const denyMutation = useDenyAccessRequest();
  const canDecide = request !== undefined && request.status === "pending";

  const handleCommentChange = (event: ChangeEvent<HTMLInputElement>): void => {
    onCommentChange(event.target.value);
  };

  const handleApprove = (): void => {
    if (request === undefined) {
      return;
    }
    approveMutation.mutate(
      { id: request.id, comment },
      { onSuccess: (result) => onApproved(result) },
    );
  };

  const handleDeny = (): void => {
    if (request === undefined) {
      return;
    }
    denyMutation.mutate({ id: request.id, comment });
  };

  const errorMessage =
    getErrorMessage(approveMutation.error) ?? getErrorMessage(denyMutation.error);

  return (
    <Paper elevation={1} sx={{ p: 3 }}>
      <Stack sx={{ gap: 2 }}>
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
            Action failed. Check the admin session or request status. {errorMessage}
          </Alert>
        )}
        <Stack direction={{ xs: "column", sm: "row" }} sx={{ gap: 2 }}>
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
          <Alert severity="success">Approved. Session expires at {approvalResult.expiresAt}.</Alert>
        )}
        <SessionLifecycleCard.Root session={session} isLoading={isSessionLoading}>
          <SessionLifecycleCard.Header />
          <SessionLifecycleCard.StatusBadge />
          <SessionLifecycleCard.Budget />
          <SessionLifecycleCard.Capabilities />
          <SessionLifecycleCard.Timestamps />
          <SessionLifecycleCard.Actions />
        </SessionLifecycleCard.Root>
      </Stack>
    </Paper>
  );
};
