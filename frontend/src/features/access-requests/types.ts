import type { z } from "zod";
import type {
  accessRequestDetailsSchema,
  accessRequestSummarySchema,
  approvalResultSchema,
  auditEventSchema,
  denialResultSchema,
  listAuditEventsResponseSchema,
  sessionActionResultSchema,
  sessionDetailsSchema,
  sessionLifecycleResponseSchema,
} from "./schemas";

export type AccessRequestSummary = z.infer<typeof accessRequestSummarySchema>;
export type AccessRequestDetails = z.infer<typeof accessRequestDetailsSchema>;
export type ApprovalResult = z.infer<typeof approvalResultSchema>;
export type DenialResult = z.infer<typeof denialResultSchema>;
export type SessionDetails = z.infer<typeof sessionDetailsSchema>;
export type SessionActionResult = z.infer<typeof sessionActionResultSchema>;
export type SessionLifecycleResponse = z.infer<typeof sessionLifecycleResponseSchema>;
export type AuditEvent = z.infer<typeof auditEventSchema>;
export type ListAuditEventsResponse = z.infer<typeof listAuditEventsResponseSchema>;

export interface AuditEventFilters {
  readonly aggregateId?: string;
  readonly eventType?: string;
  readonly from?: string;
  readonly to?: string;
  readonly cursor?: string;
  readonly limit?: number;
}
