import { useCallback, useEffect, useState } from "react";

const storageKey = "agent-conversation";

export function useChatConversationId() {
  const [conversationId, setConversationId] = useState<string | null>(load);

  useEffect(() => {
    try {
      if (conversationId === null) {
        sessionStorage.removeItem(storageKey);
      } else {
        sessionStorage.setItem(storageKey, conversationId);
      }
    } catch {
    }
  }, [conversationId]);

  const clear = useCallback(() => setConversationId(null), []);

  return { conversationId, setConversationId, clear };
}

function load(): string | null {
  try {
    return sessionStorage.getItem(storageKey);
  } catch {
    return null;
  }
}
