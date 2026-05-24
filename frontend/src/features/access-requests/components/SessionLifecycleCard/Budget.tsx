import LinearProgress from "@mui/material/LinearProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import { useSessionLifecycleCardContext } from "./context";

export const Budget: FC = () => {
  const { session } = useSessionLifecycleCardContext();

  if (session === undefined) {
    return null;
  }

  const budgetPercent =
    session.maxActionCount === 0 ? 0 : (session.actionCount / session.maxActionCount) * 100;

  return (
    <Stack sx={{ gap: 0.75 }}>
      <Typography variant="body2">
        Actions used: {session.actionCount} / {session.maxActionCount}
      </Typography>
      <LinearProgress
        aria-label="Session action budget used"
        value={budgetPercent}
        variant="determinate"
      />
    </Stack>
  );
};
