import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import { useSessionLifecycleCardContext } from "./context";

export const Header: FC = () => {
  const { session } = useSessionLifecycleCardContext();

  if (session === undefined) {
    return null;
  }

  return (
    <Stack sx={{ gap: 0.5 }}>
      <Typography component="h3" variant="h6">
        Session {session.id}
      </Typography>
      <Typography color="text.secondary" variant="body2">
        Access request {session.accessRequestId}
      </Typography>
    </Stack>
  );
};
