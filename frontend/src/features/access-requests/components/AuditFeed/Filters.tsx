import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import type { ChangeEvent, FC } from "react";
import { type AuditFeedFilterValues, useAuditFeedContext } from "./context";

const updateTextFilter = (
  filters: AuditFeedFilterValues,
  key: "aggregateId" | "eventType" | "from" | "to",
  value: string,
): AuditFeedFilterValues => ({ ...filters, [key]: value });

const parseLimit = (value: string): number => {
  const parsedValue = Number.parseInt(value, 10);
  if (Number.isNaN(parsedValue) || parsedValue < 1) {
    return 20;
  }
  return parsedValue;
};

export const Filters: FC = () => {
  const { filters, setFilters, resetCursor } = useAuditFeedContext();

  const handleTextFilterChange =
    (key: "aggregateId" | "eventType" | "from" | "to") =>
    (event: ChangeEvent<HTMLInputElement>): void => {
      const nextValue = event.target.value;
      setFilters((currentFilters) => updateTextFilter(currentFilters, key, nextValue));
      resetCursor();
    };

  const handleLimitChange = (event: ChangeEvent<HTMLInputElement>): void => {
    const nextLimit = parseLimit(event.target.value);
    setFilters((currentFilters) => ({ ...currentFilters, limit: nextLimit }));
    resetCursor();
  };

  return (
    <Stack direction="row" sx={{ flexWrap: "wrap", gap: 2 }}>
      <TextField
        label="Aggregate ID"
        size="small"
        value={filters.aggregateId}
        onChange={handleTextFilterChange("aggregateId")}
      />
      <TextField
        label="Event type"
        size="small"
        value={filters.eventType}
        onChange={handleTextFilterChange("eventType")}
      />
      <TextField
        label="From"
        size="small"
        value={filters.from}
        onChange={handleTextFilterChange("from")}
      />
      <TextField
        label="To"
        size="small"
        value={filters.to}
        onChange={handleTextFilterChange("to")}
      />
      <TextField
        inputProps={{ min: 1 }}
        label="Limit"
        size="small"
        type="number"
        value={filters.limit}
        onChange={handleLimitChange}
      />
    </Stack>
  );
};
