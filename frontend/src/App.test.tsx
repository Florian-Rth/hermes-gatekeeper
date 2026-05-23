import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "@/App";

const emptyListResponse = { items: [] };

describe("App", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders the approval dashboard", async (): Promise<void> => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => Promise.resolve(new Response(JSON.stringify(emptyListResponse)))),
    );

    render(<App />);

    expect(
      screen.getByRole("heading", { level: 1, name: "Hermes Gatekeeper" }),
    ).toBeInTheDocument();
    expect(
      await screen.findByRole("heading", { level: 2, name: "Access requests" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { level: 2, name: "Admin approval token" }),
    ).toBeInTheDocument();
  });
});
