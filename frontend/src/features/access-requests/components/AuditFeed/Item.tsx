import Chip from "@mui/material/Chip";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import type { AuditEvent } from "../../types";
import { Details } from "./Details";

interface ItemProps {
  readonly event: AuditEvent;
}

export const Item: FC<ItemProps> = ({ event }) => (
  <Paper component="li" elevation={0} sx={{ border: 1, borderColor: "divider", p: 2 }}>
    <Stack sx={{ gap: 1 }}>
      <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", gap: 1 }}>
        <Chip color="primary" label={event.eventType} size="small" />
        <Typography color="text.secondary" variant="body2">
          {event.occurredAt}
        </Typography>
      </Stack>
      <Stack direction="row" sx={{ gap: 1 }}>
        <Typography color="text.secondary" variant="body2">
          Aggregate:
        </Typography>
        <Typography variant="body2">{event.aggregateId ?? "none"}</Typography>
      </Stack>
      <Details event={event} />
    </Stack>
  </Paper>
);
