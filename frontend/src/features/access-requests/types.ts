import type { z } from "zod";
import type {
  accessRequestDetailsSchema,
  accessRequestSummarySchema,
  approvalResultSchema,
  denialResultSchema,
  sessionActionResultSchema,
  sessionDetailsSchema,
} from "./schemas";

export type AccessRequestSummary = z.infer<typeof accessRequestSummarySchema>;
export type AccessRequestDetails = z.infer<typeof accessRequestDetailsSchema>;
export type ApprovalResult = z.infer<typeof approvalResultSchema>;
export type DenialResult = z.infer<typeof denialResultSchema>;
export type SessionDetails = z.infer<typeof sessionDetailsSchema>;
export type SessionActionResult = z.infer<typeof sessionActionResultSchema>;
