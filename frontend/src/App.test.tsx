import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "@/App";
import { queryClient } from "@/lib/queryClient";

const emptyListResponse = { items: [] };
const authenticatedSessionResponse = { authenticated: true, username: "admin" };

describe("App", () => {
  afterEach(() => {
    queryClient.clear();
    vi.unstubAllGlobals();
  });

  it("renders the approval dashboard", async (): Promise<void> => {
    vi.stubGlobal(
      "fetch",
      vi.fn((input: RequestInfo | URL) => {
        if (input.toString() === "/api/v1/admin/me") {
          return Promise.resolve(new Response(JSON.stringify(authenticatedSessionResponse)));
        }
        return Promise.resolve(new Response(JSON.stringify(emptyListResponse)));
      }),
    );

    render(<App />);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Hermes Gatekeeper" }),
    ).toBeInTheDocument();
    expect(
      await screen.findByRole("heading", { level: 2, name: "Access requests" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Abmelden" })).toBeInTheDocument();
  });
});
