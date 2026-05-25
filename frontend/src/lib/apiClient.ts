import type { z } from "zod";

interface ApiRequestOptions {
  readonly method?: "GET" | "POST";
  readonly body?: object;
  readonly headers?: Record<string, string>;
}

export class ApiError extends Error {
  public readonly status: number;

  public constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export const requestJson = async <Schema extends z.ZodType>(
  path: string,
  schema: Schema,
  options: ApiRequestOptions = {},
): Promise<z.infer<Schema>> => {
  const response = await fetch(path, {
    method: options.method ?? "GET",
    credentials: "same-origin",
    headers: {
      Accept: "application/json",
      ...(options.body === undefined ? {} : { "Content-Type": "application/json" }),
      ...options.headers,
    },
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  });

  if (!response.ok) {
    const fallback = `Request failed with status ${response.status}.`;
    const message = response.status === 401 ? "Admin session missing or expired." : fallback;
    throw new ApiError(response.status, message);
  }

  const data: z.input<Schema> = await response.json();
  return schema.parse(data);
};
