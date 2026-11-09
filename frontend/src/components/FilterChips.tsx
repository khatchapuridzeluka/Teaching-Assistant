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
      <span className="mr-1 text-[11px] font-semibold uppercase tracking-[0.06em] text-ink-muted">{label}</span>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={String(option.value ?? "all")}
            onClick={() => onChange(option.value)}
            aria-pressed={selected}
            className={`inline-flex h-7 items-center gap-1.5 rounded-full border px-3 text-xs font-semibold ${
              selected ? "border-accent bg-accent text-white" : "border-line bg-white text-ink hover:border-accent"
            }`}
          >
            {option.label}
            {option.count != null && <span className={selected ? "text-white/80" : "text-ink-muted"}>{option.count}</span>}
          </button>
        );
      })}
    </div>
  );
}
