import { type ButtonHTMLAttributes, type ReactNode, useEffect, useState } from "react";
import { Link } from "react-router";
import { ApiError, studentPhotoUrl } from "../api/client";

type ButtonVariant = "primary" | "dark" | "outline";

const buttonStyles: Record<ButtonVariant, string> = {
  primary: "bg-accent border-accent text-white hover:bg-accent/90",
  dark: "bg-ink border-ink text-white hover:bg-ink/90",
  outline: "bg-white border-line text-ink hover:bg-paper",
};

export function Button({
  variant = "outline",
  small,
  className = "",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant; small?: boolean }) {
  return (
    <button
      {...props}
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-lg border font-semibold transition-colors
        disabled:cursor-not-allowed disabled:opacity-50 ${small ? "h-7 px-2.5 text-xs" : "h-[34px] px-3.5"}
        ${buttonStyles[variant]} ${className}`}
    />
  );
}

export type ChipTone = "outline" | "tint" | "accent" | "ink" | "muted" | "dashed";

const chipStyles: Record<ChipTone, string> = {
  outline: "border-ink text-ink",
  tint: "border-transparent bg-accent-tint text-accent",
  accent: "border-transparent bg-accent text-white",
  ink: "border-transparent bg-ink text-white",
  muted: "border-transparent bg-paper-dark text-ink-muted",
  dashed: "border-dashed border-ink-muted text-ink",
};

export function Chip({ tone, children }: { tone: ChipTone; children: ReactNode }) {
  return (
    <span className={`inline-flex h-[22px] items-center whitespace-nowrap rounded-full border px-2.5 text-[11px] font-semibold ${chipStyles[tone]}`}>
      {children}
    </span>
  );
}

export function Card({ children, className = "" }: { children: ReactNode; className?: string }) {
  return <div className={`min-w-0 rounded-[10px] border border-line bg-white ${className}`}>{children}</div>;
}

export function CardHeader({ title, subtitle, actions }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-line px-[18px] py-[13px]">
      <div className="min-w-0">
        <h3 className="text-sm font-bold">{title}</h3>
        {subtitle && <div className="text-xs text-ink-muted">{subtitle}</div>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}

export function PageHeader({ title, subtitle, actions }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-3">
      <div className="min-w-0">
        <h1 className="text-[20px] font-bold tracking-tight sm:text-[22px]">{title}</h1>
        {subtitle && <div className="mt-0.5 text-ink-muted">{subtitle}</div>}
      </div>
      {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
    </div>
  );
}

export function Spinner({ label = "Loading…" }: { label?: string }) {
  return (
    <div className="flex items-center gap-2 text-ink-muted">
      <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-line border-t-accent" />
      {label}
    </div>
  );
}

export function EmptyState({ children }: { children: ReactNode }) {
  return <div className="px-[18px] py-10 text-center text-ink-muted">{children}</div>;
}

export function ErrorPanel({ error }: { error: unknown }) {
  const message = error instanceof Error ? error.message : "Something went wrong.";
  const notConnected = error instanceof ApiError && error.isNotConnected;

  return (
    <Card className="border-l-4 border-l-ink px-[18px] py-3.5">
      <div className="font-semibold">{notConnected ? "Google Classroom is not connected" : "Request failed"}</div>
      <div className="mt-0.5 text-ink-muted">{message}</div>
      {notConnected && (
        <Link to="/settings" className="mt-2 inline-block font-semibold text-accent underline">
          Go to Settings to connect
        </Link>
      )}
    </Card>
  );
}

const missingPhotos = new Set<string>();

export function forgetMissingPhotos() {
  missingPhotos.clear();
}

export function Avatar({ text, userId }: { text: string; userId?: string }) {
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
        className="h-[30px] w-[30px] shrink-0 rounded-full bg-accent-tint object-cover"
      />
    );
  }

  return (
    <span className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-accent-tint text-[11px] font-bold text-accent">
      {text}
    </span>
  );
}
