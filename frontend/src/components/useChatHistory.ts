import { useEffect, useState } from "react";
import type { ChatMessage } from "../api/chat";

export type Turn = ChatMessage & { error?: boolean };

type Conversation = { id: string; turns: Turn[] };

const storageKey = "agent-chat";

export function useChatHistory() {
  const [conversation, setConversation] = useState<Conversation>(load);

  useEffect(() => {
    try {
      sessionStorage.setItem(storageKey, JSON.stringify(conversation));
    } catch {
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
  }
  return newConversation();
}

function newConversation(): Conversation {
  return { id: crypto.randomUUID(), turns: [] };
}
