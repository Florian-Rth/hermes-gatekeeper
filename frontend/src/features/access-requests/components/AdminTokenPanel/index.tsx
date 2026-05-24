import Alert from "@mui/material/Alert";
import Paper from "@mui/material/Paper";
import Stack from "@mui/material/Stack";
import TextField from "@mui/material/TextField";
import Typography from "@mui/material/Typography";
import type { ChangeEvent, FC } from "react";
import { useAdminToken } from "../../admin-token-context";

export const AdminTokenPanel: FC = () => {
  const { adminToken, setAdminToken } = useAdminToken();

  const handleChange = (event: ChangeEvent<HTMLInputElement>): void => {
    setAdminToken(event.target.value);
  };

  return (
    <Paper elevation={1} sx={{ p: 3 }}>
      <Stack spacing={2}>
        <Typography component="h2" variant="h5">
          Admin approval token
        </Typography>
        <Alert severity="info">
          Token is kept in React state only. It is not saved to localStorage or sessionStorage.
        </Alert>
        <TextField
          fullWidth
          label="X-Gatekeeper-Admin-Token"
          type="password"
          value={adminToken}
          onChange={handleChange}
          helperText="Required only when approving or denying requests."
        />
      </Stack>
    </Paper>
  );
};
