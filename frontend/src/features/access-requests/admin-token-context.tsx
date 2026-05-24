import type { FC, ReactNode } from "react";
import { createContext, useCallback, useContext, useState } from "react";

interface AdminTokenContextValue {
  readonly adminToken: string;
  readonly adminTokenVersion: number;
  readonly setAdminToken: (value: string) => void;
}

interface AdminTokenProviderProps {
  readonly children: ReactNode;
}

const AdminTokenContext = createContext<AdminTokenContextValue | null>(null);

export const AdminTokenProvider: FC<AdminTokenProviderProps> = ({ children }) => {
  const [adminTokenState, setAdminTokenState] = useState({ token: "", version: 0 });
  const setAdminToken = useCallback((value: string): void => {
    setAdminTokenState((currentState) => {
      if (currentState.token === value) {
        return currentState;
      }
      return { token: value, version: currentState.version + 1 };
    });
  }, []);

  return (
    <AdminTokenContext.Provider
      value={{
        adminToken: adminTokenState.token,
        adminTokenVersion: adminTokenState.version,
        setAdminToken,
      }}
    >
      {children}
    </AdminTokenContext.Provider>
  );
};

export const useAdminToken = (): AdminTokenContextValue => {
  const context = useContext(AdminTokenContext);

  if (context === null) {
    throw new Error("useAdminToken must be used within AdminTokenProvider");
  }

  return context;
};
