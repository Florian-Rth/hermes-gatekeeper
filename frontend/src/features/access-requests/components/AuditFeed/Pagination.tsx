import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import { useAuditFeedContext } from "./context";

export const Pagination: FC = () => {
  const {
    adminTokenIsMissing,
    canGoBack,
    events,
    goToNextPage,
    goToPreviousPage,
    isError,
    isLoading,
    nextCursor,
  } = useAuditFeedContext();

  if (adminTokenIsMissing || isError || isLoading || (events.length === 0 && nextCursor === null)) {
    return null;
  }

  return (
    <Stack direction="row" sx={{ alignItems: "center", gap: 1 }}>
      <Button disabled={!canGoBack} onClick={goToPreviousPage} variant="outlined">
        Previous page
      </Button>
      <Button disabled={nextCursor === null} onClick={goToNextPage} variant="outlined">
        Next page
      </Button>
      <Typography color="text.secondary" variant="body2">
        {nextCursor === null ? "End of results" : "More audit events are available"}
      </Typography>
    </Stack>
  );
};
