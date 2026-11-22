import { type ButtonHTMLAttributes, type ReactNode, type Ref, useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { CircleCheck, Info, Search, TriangleAlert, X } from "lucide-react";
import { ApiError, studentPhotoUrl } from "../api/client";
import { avatarTone } from "../theme";

type ButtonVariant = "primary" | "dark" | "outline" | "ghost" | "danger" | "mark" | "success";

const buttonStyles: Record<ButtonVariant, string> = {
  primary: "border-accent bg-accent text-white shadow-[inset_0_-1px_0_rgb(0_0_0/0.16)] hover:border-accent-strong hover:bg-accent-strong",
  dark: "border-navy bg-navy text-white hover:bg-navy-soft",
  outline: "border-line bg-white text-ink shadow-card hover:border-ink-faint hover:bg-paper",
  ghost: "border-transparent bg-transparent text-ink-soft hover:bg-paper-dark",
  danger: "border-danger bg-danger text-white hover:border-danger-ink hover:bg-danger-ink",
  success: "border-success bg-success text-white shadow-[inset_0_-1px_0_rgb(0_0_0/0.16)] hover:border-success-ink hover:bg-success-ink",
  mark: "border-mark bg-mark text-white shadow-[inset_0_-1px_0_rgb(0_0_0/0.12)] hover:brightness-95",
};

export function buttonClass(variant: ButtonVariant = "outline", small = false, className = "") {
  return `inline-flex shrink-0 items-center justify-center gap-1.5 whitespace-nowrap rounded-[7px] border font-semibold transition-[background-color,border-color,transform,filter] duration-150
    active:translate-y-px disabled:cursor-not-allowed disabled:opacity-50 disabled:active:translate-y-0 ${small ? "h-7 px-2.5 text-xs" : "h-[34px] px-3.5 text-[13px]"}
    ${buttonStyles[variant]} ${className}`;
}

export function Button({
  variant = "outline",
  small,
  icon,
  className = "",
  children,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; small?: boolean; icon?: ReactNode; ref?: Ref<HTMLButtonElement> }) {
  return (
    <button {...props} className={buttonClass(variant, small, className)}>
      {icon && <span className={`flex shrink-0 items-center ${small ? "[&>svg]:h-3.5 [&>svg]:w-3.5" : "[&>svg]:h-4 [&>svg]:w-4"}`}>{icon}</span>}
      {children}
    </button>
  );
}

export type ChipTone = "outline" | "tint" | "info" | "accent" | "success" | "ink" | "muted" | "dashed" | "warn" | "danger" | "mark";

const chipStyles: Record<ChipTone, string> = {
  outline: "border-line bg-white text-ink-soft",
  tint: "border-transparent bg-accent-tint text-accent-strong",
  info: "border-transparent bg-accent-tint text-accent-strong",
  accent: "border-transparent bg-success-tint text-success-ink",
  success: "border-transparent bg-success-tint text-success-ink",
  ink: "border-transparent bg-navy text-white",
  muted: "border-transparent bg-paper-dark text-ink-muted",
  dashed: "border-dashed border-ink-faint bg-transparent text-ink-muted",
  warn: "border-transparent bg-warn-tint text-warn-ink",
  danger: "border-transparent bg-danger-tint text-danger-ink",
  mark: "border-transparent bg-mark-tint text-mark-ink",
};

export function Chip({ tone, children, icon, className = "" }: { tone: ChipTone; children: ReactNode; icon?: ReactNode; className?: string }) {
  return (
    <span
      className={`inline-flex h-[21px] items-center gap-1 whitespace-nowrap rounded-[5px] border px-1.5 text-[11px] font-semibold leading-none ${chipStyles[tone]} ${className}`}
    >
      {icon && <span className="flex shrink-0 items-center [&>svg]:h-3 [&>svg]:w-3">{icon}</span>}
      {children}
    </span>
  );
}

export function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`surface min-w-0 ${className}`}>{children}</div>;
}

export function CardHeader({ title, subtitle, actions, icon }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode; icon?: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-line px-[18px] py-3">
      <div className="flex min-w-0 items-center gap-2.5">
        {icon && <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-paper text-ink-muted [&>svg]:h-4 [&>svg]:w-4">{icon}</span>}
        <div className="min-w-0">
          <h3 className="text-[14px] font-semibold">{title}</h3>
          {subtitle && <div className="text-xs text-ink-muted">{subtitle}</div>}
        </div>
      </div>
      {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
    </div>
  );
}

export function PageHeader({ title, subtitle, actions, lead }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode; lead?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
      <div className="flex min-w-0 items-center gap-3.5">
        {lead}
        <div className="min-w-0">
          <h1 className="font-display text-[25px] font-semibold leading-[1.15] tracking-[-0.01em] sm:text-[28px]">{title}</h1>
          {subtitle && <div className="mt-1 text-ink-muted">{subtitle}</div>}
        </div>
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

const stripColumns: Record<number, string> = {
  3: "md:grid-cols-3",
  4: "md:grid-cols-4",
  5: "md:grid-cols-5",
  6: "md:grid-cols-3 xl:grid-cols-6",
};

export function StatStrip({ children, columns = 4, className = "" }: { children: ReactNode; columns?: 3 | 4 | 5 | 6; className?: string }) {
  return (
    <div className={`grid shrink-0 grid-cols-2 gap-px overflow-hidden rounded-[10px] border border-line bg-line shadow-card ${stripColumns[columns]} ${className}`}>
      {children}
    </div>
  );
}

type StatTone = "bad" | "good" | "mark";

const statTones: Record<StatTone, string> = { bad: "text-danger", good: "text-success", mark: "text-ink" };

export function Stat({
  label,
  value,
  note,
  tone,
  compact,
  icon,
}: {
  label: ReactNode;
  value: ReactNode;
  note?: ReactNode;
  tone?: StatTone;
  compact?: boolean;
  icon?: ReactNode;
}) {
  if (compact) {
    return (
      <div className="flex min-w-0 items-center gap-3 bg-white px-4 py-2.5">
        {icon && <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-paper text-ink-muted [&>svg]:h-4 [&>svg]:w-4">{icon}</span>}
        <div className="min-w-0">
          <div className="eyebrow truncate">{label}</div>
          <div className="flex items-baseline gap-2">
            <b className={`num whitespace-nowrap text-[19px] leading-tight ${tone ? statTones[tone] : ""}`}>{value}</b>
            {note && <span className="truncate text-[11px] text-ink-muted">{note}</span>}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="relative min-w-0 bg-white px-4 py-3.5">
      {tone === "mark" && <span className="absolute inset-y-0 left-0 w-[3px] bg-mark" aria-hidden />}
      <div className="flex items-center justify-between gap-2">
        <div className="eyebrow truncate" title={typeof label === "string" ? label : undefined}>
          {label}
        </div>
        {icon && <span className="flex shrink-0 items-center text-ink-faint [&>svg]:h-4 [&>svg]:w-4">{icon}</span>}
      </div>
      <div className={`num mt-1.5 truncate text-[24px] leading-none ${tone ? statTones[tone] : ""}`}>{value}</div>
      {note && (
        <div className="mt-1.5 truncate text-[11px] text-ink-muted" title={typeof note === "string" ? note : undefined}>
          {note}
        </div>
      )}
    </div>
  );
}

export function Tabs<T extends string>({
  tabs,
  value,
  onChange,
  label,
  className = "",
}: {
  tabs: { value: T; label: ReactNode; count?: number | null; icon?: ReactNode }[];
  value: T;
  onChange: (value: T) => void;
  label: string;
  className?: string;
}) {
  return (
    <nav className={`flex gap-1 overflow-x-auto border-b border-line ${className}`} role="tablist" aria-label={label}>
      {tabs.map((tab) => {
        const selected = tab.value === value;
        return (
          <button
            key={tab.value}
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(tab.value)}
            className={`-mb-px flex shrink-0 items-center gap-1.5 border-b-2 px-3 py-2 text-[13px] font-semibold transition-colors ${
              selected ? "border-accent text-accent-strong" : "border-transparent text-ink-muted hover:border-line hover:text-ink"
            }`}
          >
            {tab.icon && <span className="flex items-center [&>svg]:h-4 [&>svg]:w-4">{tab.icon}</span>}
            {tab.label}
            {tab.count != null && (
              <span className={`rounded-full px-1.5 font-mono text-[10.5px] leading-[18px] ${selected ? "bg-accent-tint text-accent-strong" : "bg-paper-dark text-ink-muted"}`}>
                {tab.count}
              </span>
            )}
          </button>
        );
      })}
    </nav>
  );
}

export function Segmented<T extends string>({
  options,
  value,
  onChange,
  label,
  stretch,
}: {
  options: { value: T; label: ReactNode; count?: number | null }[];
  value: T;
  onChange: (value: T) => void;
  label: string;
  stretch?: boolean;
}) {
  return (
    <div className={`${stretch ? "flex" : "inline-flex"} rounded-lg bg-paper-dark p-0.5`} role="tablist" aria-label={label}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={option.value}
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(option.value)}
            className={`flex min-w-0 items-center justify-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-semibold transition-all ${stretch ? "flex-1" : ""} ${
              selected ? "bg-white text-ink shadow-card" : "text-ink-muted hover:text-ink"
            }`}
          >
            <span className="truncate">{option.label}</span>
            {option.count != null && <span className={`font-mono text-[10.5px] ${selected ? "text-accent" : "text-ink-faint"}`}>{option.count}</span>}
          </button>
        );
      })}
    </div>
  );
}

export function CountTabs<T extends string>({
  options,
  value,
  onChange,
  label,
}: {
  options: { value: T; label: string; count: number }[];
  value: T;
  onChange: (value: T) => void;
  label: string;
}) {
  return (
    <div className="grid auto-cols-fr grid-flow-col gap-0.5 rounded-lg bg-paper-dark p-0.5" role="tablist" aria-label={label}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <button
            key={option.value}
            role="tab"
            aria-selected={selected}
            onClick={() => onChange(option.value)}
            className={`flex min-w-0 flex-col items-center rounded-md px-1 py-1 leading-tight transition-all ${
              selected ? "bg-white text-ink shadow-card" : "text-ink-muted hover:text-ink"
            }`}
          >
            <b className={`num text-[14px] ${selected ? "text-accent" : ""}`}>{option.count}</b>
            <span className="max-w-full truncate text-[10px] font-medium">{option.label}</span>
          </button>
        );
      })}
    </div>
  );
}

export function SearchField({ value, onChange, placeholder, className = "" }: { value: string; onChange: (value: string) => void; placeholder: string; className?: string }) {
  return (
    <label className={`relative block min-w-0 ${className}`}>
      <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-ink-faint" />
      <input value={value} onChange={(e) => onChange(e.target.value)} placeholder={placeholder} aria-label={placeholder} className="field w-full pl-8" />
    </label>
  );
}

export function Spinner({ label = "Loading…" }: { label?: string }) {
  return (
    <div className="flex items-center gap-2 text-ink-muted">
      <span className="h-3.5 w-3.5 shrink-0 animate-spin rounded-full border-2 border-line border-t-accent" />
      {label}
    </div>
  );
}

function useShowAfter(delayMs: number) {
  const [show, setShow] = useState(false);

  useEffect(() => {
    const timer = window.setTimeout(() => setShow(true), delayMs);
    return () => window.clearTimeout(timer);
  }, [delayMs]);

  return show;
}

const lineWidths = ["w-11/12", "w-9/12", "w-10/12", "w-7/12", "w-8/12"];

export function Skeleton({ className = "" }: { className?: string }) {
  return <span className={`skeleton block ${className}`} aria-hidden />;
}

function SkeletonFrame({ children, className = "" }: { children: ReactNode; className?: string }) {
  const show = useShowAfter(200);
  if (!show) {
    return null;
  }

  return (
    <div role="status" aria-busy="true" className={`animate-fade ${className}`}>
      <span className="sr-only">Loading…</span>
      {children}
    </div>
  );
}

export function SkeletonLines({ lines = 3, className = "" }: { lines?: number; className?: string }) {
  return (
    <SkeletonFrame className={`flex flex-col gap-2.5 ${className}`}>
      {Array.from({ length: lines }, (_, i) => (
        <Skeleton key={i} className={`h-3 ${lineWidths[i % lineWidths.length]}`} />
      ))}
    </SkeletonFrame>
  );
}

function SkeletonRow({ index, avatar }: { index: number; avatar?: boolean }) {
  return (
    <div className="flex items-center gap-3 px-[18px] py-3">
      {avatar && <Skeleton className="h-[30px] w-[30px] shrink-0 rounded-full" />}
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <Skeleton className={`h-3 ${index % 2 === 0 ? "w-5/12" : "w-4/12"}`} />
        <Skeleton className={`h-2.5 ${index % 3 === 0 ? "w-7/12" : "w-6/12"}`} />
      </div>
      <Skeleton className="h-[21px] w-14 shrink-0 rounded-[5px]" />
    </div>
  );
}

export function SkeletonRows({ rows = 4, avatar, className = "" }: { rows?: number; avatar?: boolean; className?: string }) {
  return (
    <SkeletonFrame className={`divide-y divide-paper-dark ${className}`}>
      {Array.from({ length: rows }, (_, i) => (
        <SkeletonRow key={i} index={i} avatar={avatar} />
      ))}
    </SkeletonFrame>
  );
}

function ListBody({ rows, avatar, header }: { rows: number; avatar?: boolean; header: boolean }) {
  return (
    <div className="surface min-w-0 overflow-hidden">
      {header && (
        <div className="flex items-center justify-between gap-4 border-b border-line px-[18px] py-3.5">
          <Skeleton className="h-3.5 w-40" />
          <Skeleton className="h-7 w-48 rounded-[7px]" />
        </div>
      )}
      <div className="divide-y divide-paper-dark">
        {Array.from({ length: rows }, (_, i) => (
          <SkeletonRow key={i} index={i} avatar={avatar} />
        ))}
      </div>
    </div>
  );
}

function TableBody({ rows, columns }: { rows: number; columns: number }) {
  const grid = { gridTemplateColumns: `minmax(140px, 2fr) repeat(${columns - 1}, minmax(0, 1fr))` };
  return (
    <div className="surface min-w-0 overflow-hidden">
      <div className="grid gap-4 border-b border-line bg-paper/60 px-[18px] py-3" style={grid}>
        {Array.from({ length: columns }, (_, i) => (
          <Skeleton key={i} className={`h-2.5 ${i === 0 ? "w-20" : "w-10"}`} />
        ))}
      </div>
      {Array.from({ length: rows }, (_, row) => (
        <div key={row} className="grid items-center gap-4 border-b border-paper-dark px-[18px] py-3 last:border-b-0" style={grid}>
          <div className="flex items-center gap-2.5">
            <Skeleton className="h-6 w-6 shrink-0 rounded-full" />
            <Skeleton className={`h-3 ${row % 2 === 0 ? "w-28" : "w-20"}`} />
          </div>
          {Array.from({ length: columns - 1 }, (_, i) => (
            <Skeleton key={i} className="h-3 w-8" />
          ))}
        </div>
      ))}
    </div>
  );
}

export function SkeletonList({ rows = 6, avatar, header = true }: { rows?: number; avatar?: boolean; header?: boolean }) {
  return (
    <SkeletonFrame>
      <ListBody rows={rows} avatar={avatar} header={header} />
    </SkeletonFrame>
  );
}

export function SkeletonCards({ count = 2, className = "grid grid-cols-1 gap-4 md:grid-cols-2" }: { count?: number; className?: string }) {
  return (
    <SkeletonFrame className={className}>
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className="surface flex flex-col gap-3 px-[18px] py-4">
          <div className="flex items-center gap-3">
            <Skeleton className="h-9 w-9 shrink-0 rounded-lg" />
            <div className="flex flex-1 flex-col gap-2">
              <Skeleton className="h-3.5 w-7/12" />
              <Skeleton className="h-2.5 w-4/12" />
            </div>
          </div>
          <Skeleton className="h-1.5 w-full rounded-full" />
          <div className="flex gap-2">
            <Skeleton className="h-[21px] w-16 rounded-[5px]" />
            <Skeleton className="h-[21px] w-20 rounded-[5px]" />
          </div>
        </div>
      ))}
    </SkeletonFrame>
  );
}

function StatsBody({ count }: { count: 3 | 4 | 5 | 6 }) {
  return (
    <div className={`grid shrink-0 grid-cols-2 gap-px overflow-hidden rounded-[10px] border border-line bg-line shadow-card ${stripColumns[count]}`}>
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className="flex flex-col gap-2.5 bg-white px-4 py-3.5">
          <Skeleton className="h-2.5 w-16" />
          <Skeleton className="h-6 w-12" />
          <Skeleton className="h-2.5 w-24" />
        </div>
      ))}
    </div>
  );
}

export function SkeletonPage({
  stats = 4,
  rows = 6,
  avatar,
  table,
}: {
  stats?: 0 | 3 | 4 | 5 | 6;
  rows?: number;
  avatar?: boolean;
  table?: { columns: number };
}) {
  return (
    <SkeletonFrame className="flex flex-col gap-4">
      {stats !== 0 && <StatsBody count={stats} />}
      {table ? <TableBody rows={rows} columns={table.columns} /> : <ListBody rows={rows} avatar={avatar} header />}
    </SkeletonFrame>
  );
}

export function EmptyState({ children, icon }: { children: ReactNode; icon?: ReactNode }) {
  return (
    <div className="flex flex-col items-center px-[18px] py-10 text-center text-ink-muted">
      {icon && (
        <span className="mb-3 flex h-11 w-11 items-center justify-center rounded-full border border-dashed border-ink-faint text-ink-faint [&>svg]:h-5 [&>svg]:w-5">
          {icon}
        </span>
      )}
      <div className="max-w-[520px]">{children}</div>
    </div>
  );
}

type NoticeTone = "info" | "success" | "warn" | "danger";

const noticeStyles: Record<NoticeTone, { box: string; icon: ReactNode }> = {
  info: { box: "border-line bg-white", icon: <Info className="text-accent" /> },
  success: { box: "border-success/30 bg-success-tint/50", icon: <CircleCheck className="text-success" /> },
  warn: { box: "border-warn/30 bg-warn-tint/50", icon: <TriangleAlert className="text-warn" /> },
  danger: { box: "border-danger/30 bg-danger-tint/50", icon: <TriangleAlert className="text-danger" /> },
};

export function Notice({
  tone = "info",
  title,
  children,
  onDismiss,
  action,
  className = "",
}: {
  tone?: NoticeTone;
  title?: ReactNode;
  children?: ReactNode;
  onDismiss?: () => void;
  action?: ReactNode;
  className?: string;
}) {
  const style = noticeStyles[tone];
  return (
    <div className={`flex shrink-0 animate-rise items-start gap-2.5 rounded-[10px] border px-3.5 py-2.5 text-xs shadow-card ${style.box} ${className}`}>
      <span className="mt-px flex shrink-0 [&>svg]:h-4 [&>svg]:w-4">{style.icon}</span>
      <div className="min-w-0 flex-1">
        {title && <b className="mr-1.5 text-ink">{title}</b>}
        <span className="text-ink-muted">{children}</span>
      </div>
      {action}
      {onDismiss && (
        <button onClick={onDismiss} aria-label="Dismiss" className="shrink-0 rounded p-0.5 text-ink-faint hover:bg-paper-dark hover:text-ink">
          <X className="h-3.5 w-3.5" />
        </button>
      )}
    </div>
  );
}

export function ErrorPanel({ error }: { error: unknown }) {
  const message = error instanceof Error ? error.message : "Something went wrong.";
  const notConnected = error instanceof ApiError && error.isNotConnected;

  return (
    <div className="flex items-start gap-2.5 rounded-[10px] border border-danger/30 bg-danger-tint/50 px-3.5 py-3">
      <TriangleAlert className="mt-px h-4 w-4 shrink-0 text-danger" />
      <div className="min-w-0">
        <div className="font-semibold text-danger-ink">{notConnected ? "Google Classroom is not connected" : "Request failed"}</div>
        <div className="mt-0.5 break-words text-ink-soft">{message}</div>
        {notConnected && (
          <Link to="/settings" className="mt-2 inline-block font-semibold text-accent underline">
            Go to Settings to connect
          </Link>
        )}
      </div>
    </div>
  );
}

const missingPhotos = new Set<string>();

export function forgetMissingPhotos() {
  missingPhotos.clear();
}

type AvatarSize = "xs" | "sm" | "md" | "lg";

const avatarSizes: Record<AvatarSize, string> = {
  xs: "h-5 w-5 text-[9px]",
  sm: "h-6 w-6 text-[10px]",
  md: "h-[30px] w-[30px] text-[11px]",
  lg: "h-11 w-11 text-[15px]",
};

export function Avatar({ text, userId, size = "md" }: { text: string; userId?: string; size?: AvatarSize }) {
  const [failed, setFailed] = useState(() => !userId || missingPhotos.has(userId));

  useEffect(() => {
    setFailed(!userId || missingPhotos.has(userId));
  }, [userId]);

  if (userId && !failed) {
    return (
      <img
        src={studentPhotoUrl(userId)}
        alt=""
        loading="lazy"
        onError={() => {
          missingPhotos.add(userId);
          setFailed(true);
        }}
        className={`shrink-0 rounded-full bg-paper-dark object-cover ring-1 ring-ink/10 ${avatarSizes[size]}`}
      />
    );
  }

  return (
    <span className={`flex shrink-0 items-center justify-center rounded-full font-bold ring-1 ring-ink/5 ${avatarSizes[size]}`} style={avatarTone(text)}>
      {text}
    </span>
  );
}

export function Meter({
  percent,
  className = "",
  tone = "accent",
}: {
  percent: number | null | undefined;
  className?: string;
  tone?: "accent" | "success" | "danger" | "mark";
}) {
  const fill = { accent: "bg-accent", success: "bg-success", danger: "bg-danger", mark: "bg-mark" }[tone];
  return (
    <span className={`block h-1.5 shrink-0 overflow-hidden rounded-full bg-paper-dark ${className}`}>
      <span className={`bar-fill block h-full rounded-full ${fill}`} style={{ width: `${Math.min(100, Math.max(0, percent ?? 0))}%` }} />
    </span>
  );
}

export function Kbd({ children }: { children: ReactNode }) {
  return (
    <kbd className="inline-flex h-[18px] min-w-[18px] items-center justify-center rounded border border-current/25 px-1 font-mono text-[10px] font-medium leading-none">
      {children}
    </kbd>
  );
}

export function useEscape(onEscape: () => void, enabled = true) {
  useEffect(() => {
    if (!enabled) {
      return;
    }
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        onEscape();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onEscape, enabled]);
}

export function Modal({
  title,
  subtitle,
  lead,
  headerExtra,
  onClose,
  children,
  footer,
  size = "max-w-2xl",
  height = "max-h-[90dvh]",
  busy,
  tinted,
  label,
}: {
  title: ReactNode;
  subtitle?: ReactNode;
  lead?: ReactNode;
  headerExtra?: ReactNode;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  size?: string;
  height?: string;
  busy?: boolean;
  tinted?: boolean;
  label?: string;
}) {
  useEscape(onClose, !busy);

  return (
    <div
      className="modal-overlay fixed inset-0 z-50 flex items-center justify-center p-2 sm:p-6"
      onClick={(e) => e.target === e.currentTarget && !busy && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-label={label ?? (typeof title === "string" ? title : undefined)}
        className={`modal-panel flex w-full flex-col overflow-hidden rounded-xl border border-line ${tinted ? "bg-paper" : "bg-white"} ${size} ${height}`}
      >
        <header className="flex shrink-0 items-start gap-3 border-b border-line bg-white px-5 py-3.5">
          {lead}
          <div className="min-w-0 flex-1">
            <h2 className="font-display text-[19px] font-semibold leading-tight">{title}</h2>
            {subtitle && <div className="mt-0.5 text-xs text-ink-muted">{subtitle}</div>}
          </div>
          {headerExtra}
          <button
            onClick={onClose}
            disabled={busy}
            aria-label="Close"
            title="Close (Esc)"
            className="-mr-1.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-ink-muted hover:bg-paper-dark hover:text-ink disabled:opacity-40"
          >
            <X className="h-4 w-4" />
          </button>
        </header>
        <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">{children}</div>
        {footer && <footer className="flex shrink-0 flex-wrap items-center justify-end gap-2 border-t border-line bg-white px-5 py-3">{footer}</footer>}
      </div>
    </div>
  );
}

export type MenuItem = {
  label: string;
  hint?: string;
  icon?: ReactNode;
  onClick?: () => void;
  href?: string;
  disabled?: boolean;
};

export function Menu({ trigger, items, label, align = "right" }: { trigger: ReactNode; items: MenuItem[]; label: string; align?: "left" | "right" }) {
  const [open, setOpen] = useState(false);
  const root = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }
    const onDown = (e: MouseEvent) => {
      if (!root.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        setOpen(false);
      }
    };
    window.addEventListener("mousedown", onDown);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("mousedown", onDown);
      window.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const itemClass =
    "flex w-full items-start gap-2.5 rounded-md px-2.5 py-2 text-left hover:bg-paper disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-transparent";

  return (
    <div ref={root} className="relative">
      <button onClick={() => setOpen(!open)} aria-haspopup="menu" aria-expanded={open} aria-label={label} title={label} className={buttonClass("outline", false, "px-2.5")}>
        {trigger}
      </button>
      {open && (
        <div
          role="menu"
          className={`absolute top-[calc(100%+6px)] z-30 w-[270px] animate-pop rounded-[10px] border border-line bg-white p-1 shadow-raised ${
            align === "right" ? "right-0 origin-top-right" : "left-0 origin-top-left"
          }`}
        >
          {items.map((item) => {
            const body = (
              <>
                {item.icon && <span className="mt-0.5 flex shrink-0 text-ink-muted [&>svg]:h-4 [&>svg]:w-4">{item.icon}</span>}
                <span className="min-w-0">
                  <span className="block font-semibold">{item.label}</span>
                  {item.hint && <span className="block text-[11px] leading-snug text-ink-muted">{item.hint}</span>}
                </span>
              </>
            );
            return item.href ? (
              <a key={item.label} role="menuitem" href={item.href} target="_blank" rel="noreferrer" onClick={() => setOpen(false)} className={itemClass}>
                {body}
              </a>
            ) : (
              <button
                key={item.label}
                role="menuitem"
                disabled={item.disabled}
                onClick={() => {
                  setOpen(false);
                  item.onClick?.();
                }}
                className={itemClass}
              >
                {body}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
