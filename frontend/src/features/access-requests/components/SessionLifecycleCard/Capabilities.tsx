import Chip from "@mui/material/Chip";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import { useSessionLifecycleCardContext } from "./context";

export const Capabilities: FC = () => {
  const { session } = useSessionLifecycleCardContext();

  if (session === undefined) {
    return null;
  }

  return (
    <Stack sx={{ gap: 1 }}>
      <Typography fontWeight={600} variant="body2">
        Allowed targets
      </Typography>
      <Stack direction="row" sx={{ flexWrap: "wrap", gap: 1 }}>
        {session.allowedTargets.map((target) => (
          <Chip key={target} label={target} size="small" variant="outlined" />
        ))}
      </Stack>
      <Typography fontWeight={600} variant="body2">
        Allowed capabilities
      </Typography>
      <Stack direction="row" sx={{ flexWrap: "wrap", gap: 1 }}>
        {session.allowedCapabilities.map((capability) => (
          <Chip key={capability} label={capability} size="small" />
        ))}
      </Stack>
    </Stack>
  );
};
