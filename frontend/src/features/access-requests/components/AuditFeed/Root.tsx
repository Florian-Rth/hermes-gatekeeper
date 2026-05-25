import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import type { FC, ReactNode } from "react";
import { useEffect, useState } from "react";
import { useAdminAuth } from "@/features/admin-auth";
import { useAuditEvents } from "../../api";
import type { AuditEventFilters } from "../../types";
import { AuditFeedContext, type AuditFeedFilterValues } from "./context";

interface RootProps {
  readonly children: ReactNode;
  readonly aggregateId?: string | null;
}

const defaultLimit = 20;

const appendTextFilter = (
  filters: AuditEventFilters,
  key: "aggregateId" | "eventType" | "from" | "to",
  value: string,
): AuditEventFilters => {
  const trimmedValue = value.trim();
  if (trimmedValue === "") {
    return filters;
  }
  return { ...filters, [key]: trimmedValue };
};

const buildRequestFilters = (
  filters: AuditFeedFilterValues,
  cursor: string | undefined,
): AuditEventFilters => {
  let requestFilters: AuditEventFilters = { limit: filters.limit };
  requestFilters = appendTextFilter(requestFilters, "aggregateId", filters.aggregateId);
  requestFilters = appendTextFilter(requestFilters, "eventType", filters.eventType);
  requestFilters = appendTextFilter(requestFilters, "from", filters.from);
  requestFilters = appendTextFilter(requestFilters, "to", filters.to);

  if (cursor !== undefined) {
    requestFilters = { ...requestFilters, cursor };
  }

  return requestFilters;
};

const getErrorMessage = (error: Error | null): string | null => {
  if (error === null) {
    return null;
  }
  return error.message;
};

export const Root: FC<RootProps> = ({ aggregateId = null, children }) => {
  const { session } = useAdminAuth();
  const [filters, setFilters] = useState<AuditFeedFilterValues>({
    aggregateId: aggregateId ?? "",
    eventType: "",
    from: "",
    to: "",
    limit: defaultLimit,
  });
  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const [cursorHistory, setCursorHistory] = useState<ReadonlyArray<string | undefined>>([]);
  const requestFilters = buildRequestFilters(filters, cursor);
  const query = useAuditEvents(requestFilters, session.authenticated);
  const adminTokenIsMissing = !session.authenticated;
  const events = query.data?.items ?? [];
  const nextCursor = query.data?.nextCursor ?? null;
  const isLoading = query.isLoading || query.isFetching;
  const errorMessage = getErrorMessage(query.error);

  useEffect(() => {
    setFilters((currentFilters) => ({ ...currentFilters, aggregateId: aggregateId ?? "" }));
    setCursor(undefined);
    setCursorHistory([]);
  }, [aggregateId]);

  const resetCursor = (): void => {
    setCursor(undefined);
    setCursorHistory([]);
  };

  const goToNextPage = (): void => {
    if (nextCursor === null) {
      return;
    }
    setCursorHistory((currentHistory) => [...currentHistory, cursor]);
    setCursor(nextCursor);
  };

  const goToPreviousPage = (): void => {
    setCursorHistory((currentHistory) => {
      if (currentHistory.length === 0) {
        return currentHistory;
      }
      const previousCursor = currentHistory[currentHistory.length - 1];
      setCursor(previousCursor);
      return currentHistory.slice(0, -1);
    });
  };

  return (
    <AuditFeedContext.Provider
      value={{
        adminTokenIsMissing,
        filters,
        requestFilters,
        events,
        nextCursor,
        isLoading,
        isError: query.isError,
        errorMessage,
        hasLoaded: query.data !== undefined,
        canGoBack: cursorHistory.length > 0,
        setFilters,
        goToNextPage,
        goToPreviousPage,
        resetCursor,
      }}
    >
      <Paper elevation={0} sx={{ border: 1, borderColor: "divider", p: 2 }}>
        <Stack sx={{ gap: 2 }}>{children}</Stack>
      </Paper>
    </AuditFeedContext.Provider>
  );
};
