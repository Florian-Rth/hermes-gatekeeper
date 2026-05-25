import { createContext, useContext } from "react";
import type { SessionDetails } from "../../types";

type DummyCapability = "test.echo" | "test.status.read";

interface SessionLifecycleCardContextValue {
  readonly session: SessionDetails | undefined;
  readonly isLoading: boolean;
  readonly isActive: boolean;
  readonly isTerminal: boolean;
  readonly dummyCapability: DummyCapability | null;
  readonly canRevoke: boolean;
  readonly isCompleting: boolean;
  readonly isRevoking: boolean;
  readonly isRunningDummyAction: boolean;
  readonly lifecycleErrorMessage: string | null;
  readonly dummyActionMessage: string | null;
  readonly isRevokeDialogOpen: boolean;
  readonly onComplete: () => void;
  readonly onRequestRevoke: () => void;
  readonly onCancelRevoke: () => void;
  readonly onConfirmRevoke: () => void;
  readonly onRunDummyAction: () => void;
}

export const SessionLifecycleCardContext = createContext<SessionLifecycleCardContextValue | null>(
  null,
);

export const useSessionLifecycleCardContext = (): SessionLifecycleCardContextValue => {
  const context = useContext(SessionLifecycleCardContext);

  if (context === null) {
    throw new Error("SessionLifecycleCard slots must be used within SessionLifecycleCard.Root");
  }

  return context;
};
