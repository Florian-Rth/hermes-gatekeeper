import Box from "@mui/material/Box";
import Container from "@mui/material/Container";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import type { FC } from "react";
import { AppHeader } from "@/components/AppHeader";
import { appConfig } from "@/lib/appConfig";

export const HomePage: FC = () => (
  <Box component="main" sx={{ minHeight: "100vh", bgcolor: "background.default", py: 4 }}>
    <Container maxWidth="md">
      <Stack spacing={3}>
        <AppHeader title={appConfig.appName} />
        <Paper elevation={1} sx={{ p: 3 }}>
          <Stack spacing={1}>
            <Typography component="h2" variant="h5">
              Frontend Skeleton bereit
            </Typography>
            <Typography color="text.secondary" variant="body1">
              Phase 0 Slice 0.4 stellt die React-, Vite-, TypeScript-, MUI- und
              TanStack-Query-Grundlage für Hermes Gatekeeper bereit.
            </Typography>
          </Stack>
        </Paper>
      </Stack>
    </Container>
  </Box>
);
