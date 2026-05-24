import Box from "@mui/material/Box";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import type { AuditEvent } from "../../types";

interface DetailsProps {
  readonly event: AuditEvent;
}

export const Details: FC<DetailsProps> = ({ event }) => {
  const entries = Object.entries(event.details);

  if (entries.length === 0) {
    return <Typography color="text.secondary">No details recorded.</Typography>;
  }

  return (
    <Box sx={{ maxHeight: 240, overflow: "auto" }}>
      <Stack component="dl" sx={{ gap: 1, m: 0 }}>
        {entries.map(([key, value]) => (
          <Stack
            component="div"
            direction="row"
            key={key}
            sx={{ borderBottom: 1, borderColor: "divider", gap: 2, py: 0.5 }}
          >
            <Typography component="dt" sx={{ flex: "0 0 12rem", fontWeight: 700 }} variant="body2">
              {key}
            </Typography>
            <Typography component="dd" sx={{ m: 0, overflowWrap: "anywhere" }} variant="body2">
              {value}
            </Typography>
          </Stack>
        ))}
      </Stack>
    </Box>
  );
};
