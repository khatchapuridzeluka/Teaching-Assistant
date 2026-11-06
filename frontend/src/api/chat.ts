import { api, unwrap } from "./client";
import type { components } from "./schema";

export type ChatMessage = components["schemas"]["ChatMessage"];
export type ChatRequest = components["schemas"]["ChatRequest"];

/** Sends the conversation to POST /agent/chat and returns the assistant's whole reply (Markdown). */
export async function sendChat(request: ChatRequest, signal: AbortSignal): Promise<string> {
  const reply = await unwrap(api.POST("/agent/chat", { body: request, signal }));
  return reply.text;
}
