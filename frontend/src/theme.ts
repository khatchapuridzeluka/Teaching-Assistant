// Colours that cannot come from a Tailwind class: chart fills, Monaco marks, inline styles.
// Keep them in step with the tokens in index.css.

export const palette = {
  ink: "#16203a",
  muted: "#5d6985",
  faint: "#8f99b1",
  line: "#dcdfea",
  grid: "#e9ecf3",
  accent: "#1857d6",
  accentSoft: "#a9c1f4",
  navy: "#0f1a36",
  mark: "#1857d6",
  hover: "rgba(22, 32, 58, 0.06)",
} as const;

export const severityColor = { Major: "#cf3a2b", Minor: "#e0921a", Style: "#8391ad" } as const;

export const strengthColor = "#1c8a4e";

export const chartText = { fontSize: 11, fill: palette.muted, fontFamily: "FiraGO, sans-serif" } as const;

export const chartLabel = { fontSize: 12, fill: palette.ink, fontFamily: "FiraGO, sans-serif" } as const;

const avatarTones = [
  { background: "#e7eeff", color: "#1146b4" },
  { background: "#e1f3f1", color: "#0f6b63" },
  { background: "#dcf5e6", color: "#0f5d33" },
  { background: "#ffe7e2", color: "#9c2318" },
  { background: "#e6f4fb", color: "#0d5c7d" },
  { background: "#eee9fb", color: "#4a3596" },
  { background: "#e9ecf3", color: "#2b3757" },
] as const;

// The same name always gets the same tint, so a student is recognisable across pages.
export function avatarTone(seed: string) {
  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  }
  return avatarTones[hash % avatarTones.length]!;
}

const courseTones = ["#0e7490", "#6d4bc4", "#1c8a4e", "#cf3a2b", "#1857d6", "#b86e00"] as const;

// A course keeps one strong colour everywhere it appears (sidebar, home, review queue).
export function courseTone(seed: string): string {
  const digits = seed.replace(/\D/g, "").slice(-6);
  if (digits) {
    return courseTones[Number(digits) % courseTones.length]!;
  }
  let hash = 0;
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) >>> 0;
  }
  return courseTones[hash % courseTones.length]!;
}
