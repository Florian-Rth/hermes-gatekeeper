import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import { useSessionLifecycleCardContext } from "./context";

const timestampRows = (
  createdAt: string,
  expiresAt: string,
  completedAt: string | null,
  revokedAt: string | null,
  expiredAt: string | null,
): ReadonlyArray<string> => [
  `Created: ${createdAt}`,
  `Expires: ${expiresAt}`,
  ...(completedAt === null ? [] : [`Completed: ${completedAt}`]),
  ...(revokedAt === null ? [] : [`Revoked: ${revokedAt}`]),
  ...(expiredAt === null ? [] : [`Expired: ${expiredAt}`]),
];

export const Timestamps: FC = () => {
  const { session } = useSessionLifecycleCardContext();

  if (session === undefined) {
    return null;
  }

  return (
    <Stack sx={{ gap: 0.5 }}>
      {timestampRows(
        session.createdAt,
        session.expiresAt,
        session.completedAt,
        session.revokedAt,
        session.expiredAt,
      ).map((row) => (
        <Typography key={row} color="text.secondary" variant="body2">
          {row}
        </Typography>
      ))}
    </Stack>
  );
};
