import type { FC } from "react";
import { AppProviders } from "@/lib/AppProviders";
import { HomePage } from "@/routes/HomePage";

export const App: FC = () => (
  <AppProviders>
    <HomePage />
  </AppProviders>
);
