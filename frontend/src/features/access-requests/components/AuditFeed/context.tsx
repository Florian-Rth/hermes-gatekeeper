import type { Dispatch, SetStateAction } from "react";
import { createContext, useContext } from "react";
import type { AuditEvent, AuditEventFilters } from "../../types";

export interface AuditFeedFilterValues {
  readonly aggregateId: string;
  readonly eventType: string;
  readonly from: string;
  readonly to: string;
  readonly limit: number;
}

export interface AuditFeedContextValue {
  readonly adminTokenIsMissing: boolean;
  readonly filters: AuditFeedFilterValues;
  readonly requestFilters: AuditEventFilters;
  readonly events: ReadonlyArray<AuditEvent>;
  readonly nextCursor: string | null;
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly errorMessage: string | null;
  readonly hasLoaded: boolean;
  readonly canGoBack: boolean;
  readonly setFilters: Dispatch<SetStateAction<AuditFeedFilterValues>>;
  readonly goToNextPage: () => void;
  readonly goToPreviousPage: () => void;
  readonly resetCursor: () => void;
}

export const AuditFeedContext = createContext<AuditFeedContextValue | null>(null);

export const useAuditFeedContext = (): AuditFeedContextValue => {
  const context = useContext(AuditFeedContext);

  if (context === null) {
    throw new Error("useAuditFeedContext must be used within AuditFeed.Root");
  }

  return context;
};
