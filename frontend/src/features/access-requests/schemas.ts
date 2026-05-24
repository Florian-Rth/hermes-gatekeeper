import { z } from "zod";

const stringListSchema = z.array(z.string());
const dateTimeSchema = z.string().min(1);
const metadataSchema = z.record(z.string(), z.string());
const actionValueSchema = z.union([z.string(), z.number(), z.boolean(), z.null()]);
const lifecycleTimestampSchema = dateTimeSchema.nullable();

export const accessRequestStatusSchema = z.enum(["pending", "approved", "denied"]);
export const riskLevelSchema = z.enum(["low", "medium", "high", "critical"]);
export const sessionStatusSchema = z.enum(["active", "completed", "revoked", "expired"]);

export const accessRequestSummarySchema = z.object({
  id: z.string().uuid(),
  intent: z.string(),
  requester: z.string(),
  targets: stringListSchema,
  requestedCapabilities: stringListSchema,
  durationMinutes: z.number().int(),
  risk: riskLevelSchema,
  status: accessRequestStatusSchema,
  createdAt: dateTimeSchema,
  updatedAt: dateTimeSchema,
});

export const listAccessRequestsResponseSchema = z.object({
  items: z.array(accessRequestSummarySchema),
});

export const accessRequestDetailsSchema = accessRequestSummarySchema.extend({
  justification: z.string().nullable(),
  proposedActions: stringListSchema,
  forbiddenActions: stringListSchema,
  metadata: metadataSchema,
});

export const approvalResultSchema = z.object({
  accessRequestId: z.string().uuid(),
  status: accessRequestStatusSchema,
  sessionId: z.string().uuid(),
  expiresAt: dateTimeSchema,
});

export const denialResultSchema = z.object({
  accessRequestId: z.string().uuid(),
  status: accessRequestStatusSchema,
});

export const sessionDetailsSchema = z.object({
  id: z.string().uuid(),
  accessRequestId: z.string().uuid(),
  status: sessionStatusSchema,
  allowedTargets: stringListSchema,
  allowedCapabilities: stringListSchema,
  createdAt: dateTimeSchema,
  expiresAt: dateTimeSchema,
  actionCount: z.number().int(),
  maxActionCount: z.number().int(),
  completedAt: lifecycleTimestampSchema,
  revokedAt: lifecycleTimestampSchema,
  expiredAt: lifecycleTimestampSchema,
});

export const sessionLifecycleResponseSchema = z.object({
  id: z.string().uuid(),
  accessRequestId: z.string().uuid(),
  status: sessionStatusSchema,
  createdAt: dateTimeSchema,
  expiresAt: dateTimeSchema,
  completedAt: lifecycleTimestampSchema,
  revokedAt: lifecycleTimestampSchema,
  expiredAt: lifecycleTimestampSchema,
});

export const sessionActionResultSchema = z.object({
  sessionId: z.string().uuid(),
  capability: z.string(),
  status: z.string(),
  result: z.record(z.string(), actionValueSchema),
});

export const auditEventSchema = z.object({
  id: z.string().uuid(),
  eventType: z.string(),
  aggregateId: z.string().uuid().nullable(),
  occurredAt: dateTimeSchema,
  details: z.record(z.string(), z.string()),
});

export const listAuditEventsResponseSchema = z.object({
  items: z.array(auditEventSchema),
  nextCursor: z.string().nullable(),
});
