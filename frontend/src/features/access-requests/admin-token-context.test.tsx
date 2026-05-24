import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { FC } from "react";
import { describe, expect, it } from "vitest";
import { AdminTokenProvider, useAdminToken } from "./admin-token-context";

const AdminTokenConsumer: FC = () => {
  const { adminToken, adminTokenVersion, setAdminToken } = useAdminToken();

  return (
    <>
      <label>
        Admin token
        <input value={adminToken} onChange={(event) => setAdminToken(event.target.value)} />
      </label>
      <output aria-label="admin token version">{adminTokenVersion}</output>
      <button type="button" onClick={() => setAdminToken(adminToken)}>
        Save unchanged token
      </button>
    </>
  );
};

describe("admin token context", () => {
  it("throws when the hook is used outside the provider", (): void => {
    expect(() => render(<AdminTokenConsumer />)).toThrow(
      "useAdminToken must be used within AdminTokenProvider",
    );
  });

  it("keeps the admin token in provider memory", async (): Promise<void> => {
    const user = userEvent.setup();
    const { getByLabelText, getByRole } = render(
      <AdminTokenProvider>
        <AdminTokenConsumer />
      </AdminTokenProvider>,
    );

    expect(getByLabelText("admin token version")).toHaveTextContent("0");

    await user.type(getByLabelText("Admin token"), "secret-token");

    expect(getByLabelText("Admin token")).toHaveValue("secret-token");
    expect(getByLabelText("admin token version")).toHaveTextContent("12");

    await user.click(getByRole("button", { name: "Save unchanged token" }));

    expect(getByLabelText("admin token version")).toHaveTextContent("12");
  });
});
