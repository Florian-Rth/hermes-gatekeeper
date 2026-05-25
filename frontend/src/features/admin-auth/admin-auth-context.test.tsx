import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { FC, ReactNode } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AppHeader } from "@/components/AppHeader";
import { AdminAuthProvider, useAdminAuth } from "./admin-auth-context";
import { adminAuthKeys } from "./api";
import { AdminAuthGate } from "./components/AdminAuthGate";

const authenticatedSession = { authenticated: true, username: "admin" };
const unauthenticatedSession = { authenticated: false, username: "" };

interface TestProvidersProps {
  readonly authenticated?: boolean;
  readonly sessionExpired?: boolean;
  readonly children: ReactNode;
  readonly queryClient?: QueryClient;
}

const createTestQueryClient = (): QueryClient =>
  new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  });

const TestProviders: FC<TestProvidersProps> = ({
  authenticated = false,
  sessionExpired = false,
  children,
  queryClient,
}) => {
  const client = queryClient ?? createTestQueryClient();
  client.setQueryData(
    adminAuthKeys.me,
    authenticated ? authenticatedSession : unauthenticatedSession,
  );
  client.setQueryData(adminAuthKeys.sessionExpired, sessionExpired);

  return (
    <QueryClientProvider client={client}>
      <AdminAuthProvider>{children}</AdminAuthProvider>
    </QueryClientProvider>
  );
};

const DashboardChild: FC = () => (
  <main>
    <AppHeader title="Admin dashboard" />
    <p>Authenticated dashboard content</p>
  </main>
);

const ExpireSessionChild: FC = () => {
  const { markSessionExpired } = useAdminAuth();

  const handleExpireSession = (): void => {
    markSessionExpired();
  };

  return (
    <button onClick={handleExpireSession} type="button">
      Expire session
    </button>
  );
};

const renderAdminGate = (authenticated = false, queryClient?: QueryClient): void => {
  render(
    <TestProviders authenticated={authenticated} queryClient={queryClient}>
      <AdminAuthGate>
        <DashboardChild />
      </AdminAuthGate>
    </TestProviders>,
  );
};

describe("AdminAuthGate", () => {
  afterEach(() => {
    cleanup();
    localStorage.clear();
    sessionStorage.clear();
    vi.unstubAllGlobals();
  });

  it("shows the local login UI for unauthenticated users", (): void => {
    renderAdminGate(false);

    expect(
      screen.getByRole("heading", { level: 2, name: "Lokaler Admin-Login" }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText(/Admin-Benutzername/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Admin-Passwort/)).toBeInTheDocument();
    expect(screen.queryByText("Authenticated dashboard content")).not.toBeInTheDocument();
  });

  it("shows authenticated content after login and does not persist the password", async (): Promise<void> => {
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (input.toString() === "/api/v1/admin/login" && init?.method === "POST") {
        return Promise.resolve(new Response(JSON.stringify(authenticatedSession)));
      }
      return Promise.resolve(new Response(JSON.stringify(unauthenticatedSession)));
    });
    vi.stubGlobal("fetch", fetchMock);
    const queryClient = createTestQueryClient();
    const user = userEvent.setup();

    renderAdminGate(false, queryClient);

    await user.type(screen.getByLabelText(/Admin-Benutzername/), "admin");
    await user.type(screen.getByLabelText(/Admin-Passwort/), "super-secret-password");
    await user.click(screen.getByRole("button", { name: "Als Admin anmelden" }));

    expect(await screen.findByText("Authenticated dashboard content")).toBeInTheDocument();
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
    expect(JSON.stringify(queryClient.getQueryData(adminAuthKeys.me))).not.toContain(
      "super-secret-password",
    );
  });

  it("shows an error after a failed login", async (): Promise<void> => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() =>
        Promise.resolve(new Response(JSON.stringify({ message: "no" }), { status: 401 })),
      ),
    );
    const user = userEvent.setup();

    renderAdminGate(false);

    await user.type(screen.getByLabelText(/Admin-Benutzername/), "admin");
    await user.type(screen.getByLabelText(/Admin-Passwort/), "wrong-password");
    await user.click(screen.getByRole("button", { name: "Als Admin anmelden" }));

    expect(
      await screen.findByText("Anmeldung fehlgeschlagen. Bitte prüfe Benutzername und Passwort."),
    ).toBeInTheDocument();
    expect(screen.queryByText("Authenticated dashboard content")).not.toBeInTheDocument();
  });

  it("returns to the login state after logout without showing a session-expired warning", async (): Promise<void> => {
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
        if (input.toString() === "/api/v1/admin/logout" && init?.method === "POST") {
          return Promise.resolve(new Response(JSON.stringify(unauthenticatedSession)));
        }
        return Promise.resolve(new Response(JSON.stringify(authenticatedSession)));
      }),
    );
    const user = userEvent.setup();

    renderAdminGate(true);

    await user.click(screen.getByRole("button", { name: "Abmelden" }));

    expect(
      await screen.findByRole("heading", { level: 2, name: "Lokaler Admin-Login" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByText("Deine Admin-Sitzung ist abgelaufen. Bitte melde dich erneut an."),
    ).not.toBeInTheDocument();
  });

  it("shows a session-expired warning after explicit session expiry", async (): Promise<void> => {
    const user = userEvent.setup();

    render(
      <TestProviders authenticated>
        <AdminAuthGate>
          <ExpireSessionChild />
        </AdminAuthGate>
      </TestProviders>,
    );

    await user.click(screen.getByRole("button", { name: "Expire session" }));

    expect(
      await screen.findByRole("heading", { level: 2, name: "Lokaler Admin-Login" }),
    ).toBeInTheDocument();
    expect(
      await screen.findByText("Deine Admin-Sitzung ist abgelaufen. Bitte melde dich erneut an."),
    ).toBeInTheDocument();
  });
});
