import { fileURLToPath, URL } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const config = {
  plugins: [react()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
      "@mui/material/styles/ThemeProvider": fileURLToPath(
        new URL("./node_modules/@mui/material/styles/ThemeProvider.js", import.meta.url),
      ),
      "@mui/material/styles/createTheme": fileURLToPath(
        new URL("./node_modules/@mui/material/styles/createTheme.js", import.meta.url),
      ),
    },
  },
  build: {
    chunkSizeWarningLimit: 1000,
  },
  test: {
    environment: "jsdom",
    setupFiles: "./src/test/setup.ts",
    css: true,
  },
};

export default defineConfig(config);
