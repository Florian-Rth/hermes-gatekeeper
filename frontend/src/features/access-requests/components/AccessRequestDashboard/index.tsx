import Container from "@mui/material/Container";
import Stack from "@mui/material/Stack";
import type { FC } from "react";
import { useEffect, useState } from "react";
import { AppHeader } from "@/components/AppHeader";
import { appConfig } from "@/lib/appConfig";
import { useAccessRequestDetails, useAccessRequests, useSessionDetails } from "../../api";
import type { ApprovalResult } from "../../types";
import { AccessRequestDetails } from "../AccessRequestDetails";
import { AccessRequestList } from "../AccessRequestList";
import { AdminTokenPanel } from "../AdminTokenPanel";
import { RequestDecisionPanel } from "../RequestDecisionPanel";

const getErrorMessage = (error: Error | null): string | null => {
  if (error === null) {
    return null;
  }
  return error.message;
};

export const AccessRequestDashboard: FC = () => {
  const [adminToken, setAdminToken] = useState("");
  const [comment, setComment] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [approvalResult, setApprovalResult] = useState<ApprovalResult | null>(null);
  const requestsQuery = useAccessRequests();
  const detailsQuery = useAccessRequestDetails(selectedId);
  const sessionQuery = useSessionDetails(approvalResult?.sessionId ?? null);

  useEffect(() => {
    if (
      selectedId !== null ||
      requestsQuery.data === undefined ||
      requestsQuery.data.length === 0
    ) {
      return;
    }
    const pendingRequest = requestsQuery.data.find((request) => request.status === "pending");
    setSelectedId(pendingRequest?.id ?? requestsQuery.data[0].id);
  }, [requestsQuery.data, selectedId]);

  const handleSelect = (id: string): void => {
    setSelectedId(id);
    setApprovalResult(null);
    setComment("");
  };

  return (
    <Stack component="main" sx={{ minHeight: "100vh", bgcolor: "background.default", py: 4 }}>
      <Container maxWidth="xl">
        <Stack spacing={3}>
          <AppHeader title={appConfig.appName} />
          <AdminTokenPanel adminToken={adminToken} onAdminTokenChange={setAdminToken} />
          <Stack direction={{ xs: "column", lg: "row" }} spacing={3} alignItems="stretch">
            <Stack sx={{ width: { xs: "100%", lg: "38%" } }}>
              <AccessRequestList
                requests={requestsQuery.data ?? []}
                isLoading={requestsQuery.isLoading}
                errorMessage={getErrorMessage(requestsQuery.error)}
                selectedId={selectedId}
                onSelect={handleSelect}
              />
            </Stack>
            <Stack spacing={3} sx={{ width: { xs: "100%", lg: "62%" } }}>
              <AccessRequestDetails
                request={detailsQuery.data}
                isLoading={detailsQuery.isLoading}
                errorMessage={getErrorMessage(detailsQuery.error)}
              />
              <RequestDecisionPanel
                request={detailsQuery.data}
                adminToken={adminToken}
                comment={comment}
                onCommentChange={setComment}
                approvalResult={approvalResult}
                session={sessionQuery.data}
                isSessionLoading={sessionQuery.isLoading}
                onApproved={setApprovalResult}
              />
            </Stack>
          </Stack>
        </Stack>
      </Container>
    </Stack>
  );
};
