import { useEffect, useState } from "react";
import type { ChatMessage } from "../api/chat";

/** One message shown in the chat. Error turns (failed or stopped requests) are shown but never sent to the model. */
export type Turn = ChatMessage & { error?: boolean };

/** A conversation with an ID, so it can move to the database later under the same key. */
type Conversation = { id: string; turns: Turn[] };

const storageKey = "agent-chat";

/**
 * The chat's conversation, kept in sessionStorage: it survives a page refresh but ends when the tab is closed, and it
 * never leaves the browser except as the history sent with each question. The only place that knows where the
 * conversation is stored, so moving it to the server later changes this file alone.
 */
export function useChatHistory() {
  const [conversation, setConversation] = useState<Conversation>(load);

  // Block body on purpose: an effect may only return a cleanup function.
  useEffect(() => {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(conversation));
    } catch {
      // Storage full or blocked: the chat still works, it just won't survive a refresh.
    }
  }, [conversation]);

  return {
    turns: conversation.turns,
    add: (turn: Turn) => setConversation((current) => ({ ...current, turns: [...current.turns, turn] })),
    clear: () => setConversation(newConversation()),
  };
}

function load(): Conversation {
  try {
    const stored = JSON.parse(sessionStorage.getItem(storageKey) ?? "null") as Partial<Conversation> | null;
    if (stored && typeof stored.id === "string" && Array.isArray(stored.turns)) {
      return { id: stored.id, turns: stored.turns };
    }
  } catch {
    // Unreadable or blocked storage: start a new conversation.
  }
  return newConversation();
}

function newConversation(): Conversation {
  return { id: crypto.randomUUID(), turns: [] };
}
