import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";

interface AppHeaderProps {
  readonly title: string;
}

export const AppHeader: FC<AppHeaderProps> = ({ title }) => (
  <Stack component="header" spacing={1}>
    <Chip color="primary" label="Hermes Gatekeeper" sx={{ alignSelf: "flex-start" }} />
    <Typography component="h1" variant="h3">
      {title}
    </Typography>
    <Typography color="text.secondary" variant="body1">
      Minimale Frontend-Basis für die Gatekeeper-Oberfläche.
    </Typography>
  </Stack>
);
