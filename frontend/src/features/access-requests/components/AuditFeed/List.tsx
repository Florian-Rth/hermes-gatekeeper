import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import type { FC } from "react";
import { useAuditFeedContext } from "./context";
import { Item } from "./Item";

export const List: FC = () => {
  const { adminTokenIsMissing, events, isError, isLoading } = useAuditFeedContext();

  if (adminTokenIsMissing || isError) {
    return null;
  }

  if (isLoading) {
    return <CircularProgress aria-label="Loading audit events" size={24} />;
  }

  if (events.length === 0) {
    return null;
  }

  return (
    <Stack component="ul" sx={{ gap: 2, listStyle: "none", m: 0, p: 0 }}>
      {events.map((event) => (
        <Item event={event} key={event.id} />
      ))}
    </Stack>
  );
};
