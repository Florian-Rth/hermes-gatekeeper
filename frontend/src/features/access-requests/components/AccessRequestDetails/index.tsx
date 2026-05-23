import Chip from "@mui/material/Chip";
import CircularProgress from "@mui/material/CircularProgress";
import Divider from "@mui/material/Divider";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC, ReactNode } from "react";
import type { AccessRequestDetails as AccessRequestDetailsType } from "../../types";

interface AccessRequestDetailsProps {
  readonly request: AccessRequestDetailsType | undefined;
  readonly isLoading: boolean;
  readonly errorMessage: string | null;
}

interface DetailRowProps {
  readonly label: string;
  readonly children: ReactNode;
}

const DetailRow: FC<DetailRowProps> = ({ label, children }) => (
  <Stack spacing={0.5}>
    <Typography color="text.secondary" variant="caption">
      {label}
    </Typography>
    <Typography component="div" variant="body1">
      {children}
    </Typography>
  </Stack>
);

const ChipList: FC<{ readonly values: ReadonlyArray<string>; readonly emptyLabel: string }> = ({
  values,
  emptyLabel,
}) => (
  <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
    {values.length === 0 ? (
      <Typography color="text.secondary">{emptyLabel}</Typography>
    ) : (
      values.map((value) => <Chip key={value} label={value} size="small" />)
    )}
  </Stack>
);

export const AccessRequestDetails: FC<AccessRequestDetailsProps> = ({
  request,
  isLoading,
  errorMessage,
}) => (
  <Paper elevation={1} sx={{ p: 3 }}>
    <Stack spacing={2}>
      <Typography component="h2" variant="h5">
        Request details
      </Typography>
      {isLoading ? (
        <Stack alignItems="center" py={4}>
          <CircularProgress aria-label="Loading request details" />
        </Stack>
      ) : null}
      {errorMessage === null ? null : (
        <Typography color="error">Could not load details: {errorMessage}</Typography>
      )}
      {!isLoading && errorMessage === null && request === undefined ? (
        <Typography color="text.secondary">Select an access request to review it.</Typography>
      ) : null}
      {request === undefined ? null : (
        <Stack spacing={2}>
          <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
            <Chip color="warning" label={request.status} />
            <Chip label={`${request.risk} risk`} />
            <Chip label={`${request.durationMinutes} minutes`} />
          </Stack>
          <DetailRow label="Intent">{request.intent}</DetailRow>
          <DetailRow label="Requester">{request.requester}</DetailRow>
          <DetailRow label="Targets">
            <ChipList values={request.targets} emptyLabel="No targets supplied." />
          </DetailRow>
          <DetailRow label="Requested capabilities">
            <ChipList
              values={request.requestedCapabilities}
              emptyLabel="No capabilities supplied."
            />
          </DetailRow>
          <DetailRow label="Justification">
            {request.justification === null || request.justification.trim() === ""
              ? "No justification supplied."
              : request.justification}
          </DetailRow>
          <Divider />
          <DetailRow label="Proposed actions">
            <ChipList values={request.proposedActions} emptyLabel="No proposed actions supplied." />
          </DetailRow>
          <DetailRow label="Forbidden actions">
            <ChipList
              values={request.forbiddenActions}
              emptyLabel="No forbidden actions supplied."
            />
          </DetailRow>
          <DetailRow label="Metadata">
            <Stack spacing={0.5}>
              {Object.entries(request.metadata).length === 0 ? (
                <Typography color="text.secondary">No metadata supplied.</Typography>
              ) : (
                Object.entries(request.metadata).map(([key, value]) => (
                  <Typography key={key} variant="body2">
                    {key}: {value}
                  </Typography>
                ))
              )}
            </Stack>
          </DetailRow>
        </Stack>
      )}
    </Stack>
  </Paper>
);
