import Button from "@mui/material/Button";
import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import { useAdminAuth, useAdminLogout } from "@/features/admin-auth";

interface AppHeaderProps {
  readonly title: string;
}

export const AppHeader: FC<AppHeaderProps> = ({ title }) => {
  const { session } = useAdminAuth();
  const logoutMutation = useAdminLogout();

  const handleLogout = (): void => {
    logoutMutation.mutate();
  };

  return (
    <Stack component="header" spacing={1}>
      <Stack
        direction={{ xs: "column", sm: "row" }}
        sx={{ justifyContent: "space-between", gap: 2 }}
      >
        <Chip color="primary" label="Hermes Gatekeeper" sx={{ alignSelf: "flex-start" }} />
        {session.authenticated ? (
          <Stack direction="row" sx={{ alignItems: "center", gap: 1 }}>
            <Typography color="text.secondary" variant="body2">
              Angemeldet als {session.username}
            </Typography>
            <Button
              disabled={logoutMutation.isPending}
              onClick={handleLogout}
              size="small"
              variant="outlined"
            >
              {logoutMutation.isPending ? "Abmelden…" : "Abmelden"}
            </Button>
          </Stack>
        ) : null}
      </Stack>
      <Typography component="h1" variant="h3">
        {title}
      </Typography>
      <Typography color="text.secondary" variant="body1">
        Minimale Frontend-Basis für die Gatekeeper-Oberfläche.
      </Typography>
    </Stack>
  );
};
