export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return "–";
  }
  return new Date(value).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function formatDay(value: string | null | undefined): string {
  if (!value) {
    return "–";
  }
  return new Date(value).toLocaleDateString(undefined, { month: "short", day: "numeric" });
}

export function formatAge(value: string): string {
  const minutes = Math.max(0, Math.round((Date.now() - new Date(value).getTime()) / 60000));
  if (minutes < 60) {
    return `${minutes} min`;
  }
  const hours = Math.round(minutes / 60);
  if (hours < 24) {
    return `${hours} h`;
  }
  return `${Math.round(hours / 24)} d`;
}

export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join("");
}

export function formatPoints(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(1);
}

export function greeting(now = new Date()): string {
  const hour = now.getHours();
  return hour < 5 ? "Working late" : hour < 12 ? "Good morning" : hour < 18 ? "Good afternoon" : "Good evening";
}
