import type { z } from "zod";
import type { adminLoginRequestSchema, adminSessionSchema } from "./schemas";

export type AdminSession = z.infer<typeof adminSessionSchema>;
export type AdminLoginRequest = z.infer<typeof adminLoginRequestSchema>;
