import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import Container from "@mui/material/Container";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import type { ChangeEvent, FC, FormEvent } from "react";
import { useState } from "react";
import { AppHeader } from "@/components/AppHeader";
import { appConfig } from "@/lib/appConfig";
import { useAdminAuth } from "../../admin-auth-context";
import { useAdminLogin } from "../../api";

export const AdminLoginPanel: FC = () => {
  const loginMutation = useAdminLogin();
  const { clearSessionExpiredMessage, sessionExpiredMessage } = useAdminAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");

  const handleUsernameChange = (event: ChangeEvent<HTMLInputElement>): void => {
    setUsername(event.target.value);
  };

  const handlePasswordChange = (event: ChangeEvent<HTMLInputElement>): void => {
    setPassword(event.target.value);
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    clearSessionExpiredMessage();
    loginMutation.mutate(
      { username, password },
      {
        onSuccess: () => {
          setPassword("");
        },
      },
    );
  };

  return (
    <Stack component="main" sx={{ minHeight: "100vh", bgcolor: "background.default", py: 4 }}>
      <Container maxWidth="sm">
        <Stack sx={{ gap: 3 }}>
          <AppHeader title={appConfig.appName} />
          <Paper component="form" elevation={1} onSubmit={handleSubmit} sx={{ p: 3 }}>
            <Stack sx={{ gap: 2 }}>
              <Typography component="h2" variant="h5">
                Lokaler Admin-Login
              </Typography>
              <Typography color="text.secondary">
                Melde dich mit den lokalen Admin-Zugangsdaten an. Die Sitzung wird über ein
                HttpOnly-Cookie gehalten; Passwort und Session-Secret werden nicht im Browser
                gespeichert.
              </Typography>
              {sessionExpiredMessage === null ? null : (
                <Alert severity="warning">{sessionExpiredMessage}</Alert>
              )}
              {loginMutation.isError ? (
                <Alert severity="error">
                  Anmeldung fehlgeschlagen. Bitte prüfe Benutzername und Passwort.
                </Alert>
              ) : null}
              <TextField
                autoComplete="username"
                fullWidth
                label="Admin-Benutzername"
                name="username"
                onChange={handleUsernameChange}
                required
                value={username}
              />
              <TextField
                autoComplete="current-password"
                fullWidth
                label="Admin-Passwort"
                name="password"
                onChange={handlePasswordChange}
                required
                type="password"
                value={password}
              />
              <Button
                disabled={loginMutation.isPending}
                type="submit"
                variant="contained"
                sx={{ alignSelf: "flex-start" }}
              >
                {loginMutation.isPending ? "Anmelden…" : "Als Admin anmelden"}
              </Button>
            </Stack>
          </Paper>
        </Stack>
      </Container>
    </Stack>
  );
};
