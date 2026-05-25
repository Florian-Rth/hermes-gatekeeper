import { z } from "zod";

export const adminSessionSchema = z.object({
  authenticated: z.boolean(),
  username: z.string(),
});

export const adminLoginRequestSchema = z.object({
  username: z.string().min(1),
  password: z.string().min(1),
});
