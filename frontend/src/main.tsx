import { StrictMode } from "react";
import ReactDOM from "react-dom/client";
import { App } from "@/App";
import "@/styles/global.css";

const rootElement: HTMLElement | null = document.getElementById("root");

if (rootElement === null) {
  throw new Error("Root element with id 'root' was not found.");
}

ReactDOM.createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
