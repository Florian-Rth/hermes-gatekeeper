import createTheme from "@mui/material/styles/createTheme";

export const theme = createTheme({
  palette: {
    mode: "light",
    primary: {
      main: "#4f46e5",
    },
    background: {
      default: "#f8fafc",
    },
  },
  typography: {
    fontFamily: ["Inter", "system-ui", "sans-serif"].join(","),
  },
});
