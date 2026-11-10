export function FilterChips<T extends string | number | undefined>({
  label,
  options,
  value,
  onChange,
}: {
  label: string;
  options: { value: T; label: string; count?: number | null }[];
  value: T;
  onChange: (value: T) => void;
}) {
  return (
    <div className="flex flex-wrap items-center gap-1.5" role="group" aria-label={label}>
      <span className="eyebrow mr-1 w-[68px] shrink-0">{label}</span>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={String(option.value ?? "all")}
            onClick={() => onChange(option.value)}
            aria-pressed={selected}
            className={`inline-flex h-7 items-center gap-1.5 rounded-md border px-2.5 text-xs font-medium transition-colors ${
              selected
                ? "border-accent bg-accent-tint font-semibold text-accent-strong shadow-[inset_0_0_0_1px_var(--color-accent)]"
                : "border-line bg-white text-ink-soft hover:border-ink-faint hover:bg-paper"
            }`}
          >
            {option.label}
            {option.count != null && <span className={`font-mono text-[10.5px] ${selected ? "text-accent" : "text-ink-faint"}`}>{option.count}</span>}
          </button>
        );
      })}
    </div>
  );
}
