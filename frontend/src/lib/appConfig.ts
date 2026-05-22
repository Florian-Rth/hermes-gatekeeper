import { z } from "zod";

const appConfigSchema = z.object({
  appName: z.string().min(1),
});

export type AppConfig = z.infer<typeof appConfigSchema>;

export const appConfig: AppConfig = appConfigSchema.parse({
  appName: "Hermes Gatekeeper",
});
