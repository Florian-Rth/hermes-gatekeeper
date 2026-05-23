import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import List from "@mui/material/List";
import ListItemButton from "@mui/material/ListItemButton";
import ListItemText from "@mui/material/ListItemText";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import type { AccessRequestSummary } from "../../types";

interface AccessRequestListProps {
  readonly requests: ReadonlyArray<AccessRequestSummary>;
  readonly isLoading: boolean;
  readonly errorMessage: string | null;
  readonly selectedId: string | null;
  readonly onSelect: (id: string) => void;
}

const statusColor = (status: AccessRequestSummary["status"]): "success" | "warning" | "error" => {
  if (status === "approved") {
    return "success";
  }
  if (status === "denied") {
    return "error";
  }
  return "warning";
};

export const AccessRequestList: FC<AccessRequestListProps> = ({
  requests,
  isLoading,
  errorMessage,
  selectedId,
  onSelect,
}) => {
  const sortedRequests = [...requests].sort((left, right) => {
    if (left.status === right.status) {
      return right.createdAt.localeCompare(left.createdAt);
    }
    return left.status === "pending" ? -1 : 1;
  });

  return (
    <Paper elevation={1} sx={{ p: 2 }}>
      <Stack spacing={2}>
        <Stack direction="row" spacing={1} alignItems="center" justifyContent="space-between">
          <Typography component="h2" variant="h5">
            Access requests
          </Typography>
          <Chip label={`${requests.length} total`} size="small" />
        </Stack>
        {isLoading ? (
          <Stack alignItems="center" py={4}>
            <CircularProgress aria-label="Loading access requests" />
          </Stack>
        ) : null}
        {errorMessage === null ? null : (
          <Typography color="error">Could not load requests: {errorMessage}</Typography>
        )}
        {!isLoading && errorMessage === null && sortedRequests.length === 0 ? (
          <Typography color="text.secondary">No access requests yet.</Typography>
        ) : null}
        <List disablePadding>
          {sortedRequests.map((request) => (
            <ListItemButton
              key={request.id}
              selected={request.id === selectedId}
              onClick={() => onSelect(request.id)}
              sx={{ borderRadius: 1, mb: 1 }}
            >
              <ListItemText
                primary={request.intent}
                secondary={`${request.requester} · ${request.risk} risk · ${request.durationMinutes} min`}
              />
              <Chip color={statusColor(request.status)} label={request.status} size="small" />
            </ListItemButton>
          ))}
        </List>
      </Stack>
    </Paper>
  );
};
