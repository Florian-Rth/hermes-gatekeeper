import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { App } from "@/App";

describe("App", () => {
  it("renders the placeholder page", (): void => {
    render(<App />);

    expect(
      screen.getByRole("heading", { level: 1, name: "Hermes Gatekeeper" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { level: 2, name: "Frontend Skeleton bereit" }),
    ).toBeInTheDocument();
  });
});
