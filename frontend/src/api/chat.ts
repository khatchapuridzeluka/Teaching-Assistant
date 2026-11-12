import { useMutation, useQuery } from "@tanstack/react-query";
import { api, unwrap } from "./client";
import type { components } from "./schema";

export type AgentRun = components["schemas"]["AgentRunResponse"];
export type AgentRunStatus = components["schemas"]["AgentRunStatus"];
export type AgentMessage = components["schemas"]["AgentMessageResponse"];
export type AgentConversation = components["schemas"]["AgentConversationResponse"];
export type StartAgentRunRequest = components["schemas"]["StartAgentRunRequest"];

export const isRunFinished = (status: AgentRunStatus) => status === "Succeeded" || status === "Failed" || status === "Canceled";

export const useAgentConversation = (conversationId: string | null) =>
  useQuery({
    queryKey: ["agent", "conversations", conversationId],
    queryFn: () =>
      unwrap(api.GET("/agent/conversations/{conversationId}", { params: { path: { conversationId: conversationId ?? "" } } })),
    enabled: conversationId !== null,
    retry: false,
  });

export const useAgentRun = (runId: string | null) =>
  useQuery({
    queryKey: ["agent", "runs", runId],
    queryFn: () => unwrap(api.GET("/agent/runs/{runId}", { params: { path: { runId: runId ?? "" } } })),
    enabled: runId !== null,
    staleTime: 0,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === undefined || !isRunFinished(status) ? 1500 : false;
    },
  });

export const useCancelAgentRun = () =>
  useMutation({
    mutationFn: (runId: string) => unwrap(api.POST("/agent/runs/{runId}/cancel", { params: { path: { runId } } })),
  });

export const useStartAgentRun = () =>
  useMutation({
    mutationFn: (request: StartAgentRunRequest) => unwrap(api.POST("/agent/runs", { body: request })),
  });
