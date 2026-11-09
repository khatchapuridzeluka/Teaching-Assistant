import { type ReactNode, useState } from "react";
import { Link } from "react-router";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ModelCallItem, ModelUsageTotal } from "../api/client";
import { useModelUsage } from "../api/hooks";
import { formatDate, formatDay } from "../format";
import { Card, CardHeader, Chip, EmptyState, ErrorPanel, Spinner } from "./ui";

const series = [
  { key: "uncached", label: "Prompt, not cached", color: "#2a78d6" },
  { key: "cached", label: "Prompt, from cache", color: "#eb6834" },
  { key: "output", label: "Output", color: "#1baf7a" },
] as const;

const purposeLabels: Record<string, string> = {
  Grading: "AI grading",
  Chat: "Ask agent",
  HealthCheck: "Model check",
};

const periods = [7, 30, 90];

export function UsageView() {
  const [days, setDays] = useState(30);
  const usage = useModelUsage(days);
  const data = usage.data;
  const grading = data?.totals.find((t) => t.purpose === "Grading");
  const chartData = (data?.days ?? []).map((d) => ({
    date: d.date,
    uncached: d.inputTokens + d.cacheWriteTokens,
    cached: d.cacheReadTokens,
    output: d.outputTokens,
    calls: d.calls,
  }));
  const hasCalls = (data?.totals.length ?? 0) > 0;

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="text-xs text-ink-muted">
          Every Claude call: tokens, cache use, duration and errors · only metadata is recorded, never prompts or answers
        </div>
        <div className="flex items-center gap-1.5" role="group" aria-label="Period">
          {periods.map((p) => (
            <button
              key={p}
              onClick={() => setDays(p)}
              aria-pressed={days === p}
              className={`inline-flex h-7 items-center rounded-full border px-3 text-xs font-semibold ${
                days === p ? "border-accent bg-accent text-white" : "border-line bg-white text-ink hover:border-accent"
              }`}
            >
              {p} days
            </button>
          ))}
        </div>
      </div>

      {usage.isError && <ErrorPanel error={usage.error} />}
      {usage.isLoading && <Spinner />}

      {data && !hasCalls && (
        <Card>
          <EmptyState>
            <div className="font-semibold text-ink">No model calls in the last {days} days</div>
            Grade a submission with AI, ask the agent or run the model check; each call shows up here.
          </EmptyState>
        </Card>
      )}

      {data && hasCalls && (
        <>
          <div className="grid grid-cols-2 border-t border-line md:grid-cols-5">
            <Stat label="AI gradings" value={grading ? grading.calls : 0} note={grading?.failed ? `${grading.failed} failed` : "none failed"} />
            <Stat label="Prompt per grading" value={grading ? tokens(grading.averagePromptTokens) : "–"} note="tokens sent, on average" />
            <Stat label="Output per grading" value={grading ? tokens(grading.averageOutputTokens) : "–"} note="tokens written, on average" />
            <Stat label="From cache" value={percent(grading?.cacheHitRate)} note="share of grading prompts" />
            <Stat label="Grading time" value={grading ? seconds(grading.averageDurationMs) : "–"} note="average per call" />
          </div>

          <Card>
            <CardHeader title="Tokens per day" subtitle={`All calls, last ${days} days`} />
            <div className="px-[18px] pt-3">
              <Legend />
            </div>
            <div className="h-[240px] px-2 pb-3 pt-1">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData} margin={{ top: 8, right: 12, bottom: 0, left: 4 }} barCategoryGap="20%">
                  <CartesianGrid vertical={false} stroke="#ecebe7" />
                  <XAxis
                    dataKey="date"
                    tickFormatter={(d: string) => formatDay(d)}
                    tick={{ fontSize: 11, fill: "#6b6e70" }}
                    tickLine={false}
                    axisLine={{ stroke: "#dedcd7" }}
                    minTickGap={24}
                  />
                  <YAxis
                    tickFormatter={(v: number) => tokens(v)}
                    tick={{ fontSize: 11, fill: "#6b6e70" }}
                    tickLine={false}
                    axisLine={false}
                    width={44}
                  />
                  <Tooltip cursor={{ fill: "rgba(31,35,38,0.06)" }} content={<DayTooltip />} />
                  {series.map((s, i) => (
                    <Bar
                      key={s.key}
                      dataKey={s.key}
                      name={s.label}
                      stackId="tokens"
                      fill={s.color}
                      stroke="#ffffff"
                      strokeWidth={2}
                      maxBarSize={18}
                      radius={i === series.length - 1 ? [4, 4, 0, 0] : 0}
                      isAnimationActive={false}
                    />
                  ))}
                </BarChart>
              </ResponsiveContainer>
            </div>
            <details className="border-t border-line px-[18px] py-2.5 text-xs">
              <summary className="cursor-pointer font-semibold text-ink-muted hover:text-ink">Per day as a table</summary>
              <div className="mt-2 max-h-56 overflow-auto">
                <table className="w-full text-left tabular-nums">
                  <thead className="text-ink-muted">
                    <tr>
                      <th className="py-1 pr-3 font-semibold">Day</th>
                      <th className="py-1 pr-3 font-semibold">Calls</th>
                      {series.map((s) => (
                        <th key={s.key} className="py-1 pr-3 font-semibold">
                          {s.label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {chartData
                      .filter((d) => d.calls > 0)
                      .reverse()
                      .map((d) => (
                        <tr key={d.date} className="border-t border-line">
                          <td className="py-1 pr-3">{formatDay(d.date)}</td>
                          <td className="py-1 pr-3">{d.calls}</td>
                          <td className="py-1 pr-3">{tokens(d.uncached)}</td>
                          <td className="py-1 pr-3">{tokens(d.cached)}</td>
                          <td className="py-1 pr-3">{tokens(d.output)}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            </details>
          </Card>

          <Card>
            <CardHeader title="By purpose" subtitle="Prompt tokens include cached ones; averages count answered calls" />
            <div className="overflow-x-auto">
              <table className="w-full min-w-[640px] text-left text-xs tabular-nums">
                <thead className="text-ink-muted">
                  <tr className="border-b border-line">
                    <th className="px-[18px] py-2 font-semibold">Purpose</th>
                    <th className="px-3 py-2 font-semibold">Calls</th>
                    <th className="px-3 py-2 font-semibold">Failed</th>
                    <th className="px-3 py-2 font-semibold">Prompt tokens</th>
                    <th className="px-3 py-2 font-semibold">From cache</th>
                    <th className="px-3 py-2 font-semibold">Output tokens</th>
                    <th className="px-3 py-2 font-semibold">Avg time</th>
                  </tr>
                </thead>
                <tbody>
                  {data.totals.map((t) => (
                    <PurposeRow key={t.purpose} total={t} />
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          <Card>
            <CardHeader title="Recent calls" subtitle="Newest first · up to 200" />
            <ul className="@container divide-y divide-line">
              {data.recent.map((call) => (
                <CallRow key={call.id} call={call} />
              ))}
            </ul>
          </Card>
        </>
      )}
    </>
  );
}

function PurposeRow({ total }: { total: ModelUsageTotal }) {
  const prompt = total.inputTokens + total.cacheReadTokens + total.cacheWriteTokens;
  return (
    <tr className="border-b border-line last:border-b-0">
      <td className="px-[18px] py-2 font-semibold">{purposeLabels[total.purpose] ?? total.purpose}</td>
      <td className="px-3 py-2">{total.calls}</td>
      <td className={`px-3 py-2 ${total.failed > 0 ? "font-semibold text-[#8f2d2d]" : ""}`}>{total.failed}</td>
      <td className="px-3 py-2">{tokens(prompt)}</td>
      <td className="px-3 py-2">{percent(total.cacheHitRate)}</td>
      <td className="px-3 py-2">{tokens(total.outputTokens)}</td>
      <td className="px-3 py-2">{seconds(total.averageDurationMs)}</td>
    </tr>
  );
}

function CallRow({ call }: { call: ModelCallItem }) {
  const prompt = (call.inputTokens ?? 0) + (call.cacheReadTokens ?? 0) + (call.cacheWriteTokens ?? 0);
  const fallback = call.model != null && call.model !== call.requestedModel;
  const link =
    call.courseId && call.assignmentId && call.submissionId
      ? `/courses/${call.courseId}/assignments/${call.assignmentId}?student=${encodeURIComponent(call.submissionId)}`
      : undefined;

  return (
    <li className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-4 gap-y-1.5 px-[18px] py-2.5 text-xs @4xl:grid-cols-[minmax(0,1fr)_200px_160px_90px]">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-1.5">
          {call.succeeded ? (
            <Chip tone="outline">{purposeLabels[call.purpose] ?? call.purpose}</Chip>
          ) : (
            <span className="inline-flex h-[22px] items-center rounded-full bg-[#8f2d2d] px-2.5 text-[11px] font-semibold text-white">
              {purposeLabels[call.purpose] ?? call.purpose} failed
            </span>
          )}
          {call.studentName && (
            <span className="truncate font-semibold">
              {link ? (
                <Link to={link} className="hover:underline">
                  {call.studentName}
                </Link>
              ) : (
                call.studentName
              )}
            </span>
          )}
          {call.assignmentTitle && <span className="truncate text-ink-muted">· {call.assignmentTitle}</span>}
        </div>
        {call.error && (
          <div className="mt-0.5 line-clamp-2 text-[#8f2d2d]" title={call.error}>
            {call.error}
          </div>
        )}
      </div>
      <div className="text-right text-ink-muted @4xl:text-left" title={formatDate(call.createdAt)}>
        {formatDate(call.createdAt)} · {seconds(call.durationMs)}
      </div>
      <div className="col-span-2 tabular-nums text-ink-muted @4xl:col-span-1">
        {call.succeeded ? (
          <>
            {tokens(prompt)} in
            {call.cacheReadTokens ? ` (${Math.round((call.cacheReadTokens / Math.max(prompt, 1)) * 100)}% cached)` : ""} ·{" "}
            {tokens(call.outputTokens ?? 0)} out
          </>
        ) : (
          "no tokens"
        )}
      </div>
      <div className="col-span-2 truncate text-ink-muted @4xl:col-span-1" title={`asked for ${call.requestedModel}`}>
        {fallback ? <span className="font-semibold text-ink">{call.model} (fallback)</span> : call.model ?? call.requestedModel}
      </div>
    </li>
  );
}

function Legend() {
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-ink-muted">
      {series.map((s) => (
        <span key={s.key} className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-[3px]" style={{ backgroundColor: s.color }} />
          {s.label}
        </span>
      ))}
    </div>
  );
}

type DayTooltipProps = {
  active?: boolean;
  label?: string;
  payload?: { dataKey?: string | number; value?: number; payload?: { calls: number } }[];
};

function DayTooltip({ active, label, payload }: DayTooltipProps) {
  if (!active || !payload || payload.length === 0 || !label) {
    return null;
  }

  const calls = payload[0]?.payload?.calls ?? 0;
  return (
    <div className="rounded-lg border border-line bg-white px-3 py-2 text-xs shadow-md">
      <div className="mb-1 font-semibold">
        {formatDay(label)} · {calls} {calls === 1 ? "call" : "calls"}
      </div>
      {series.map((s) => {
        const value = payload.find((p) => p.dataKey === s.key)?.value ?? 0;
        return (
          <div key={s.key} className="flex items-center justify-between gap-4">
            <span className="inline-flex items-center gap-1.5 text-ink-muted">
              <span className="h-2 w-2 rounded-[2px]" style={{ backgroundColor: s.color }} />
              {s.label}
            </span>
            <span className="tabular-nums text-ink">{tokens(value)}</span>
          </div>
        );
      })}
    </div>
  );
}

function Stat({ label, value, note }: { label: string; value: ReactNode; note: string }) {
  return (
    <div className="border-b border-line px-4 py-3 md:border-b-0 md:border-r md:last:border-r-0">
      <div className="text-xs text-ink-muted">{label}</div>
      <div className="text-[22px] font-bold leading-tight">{value}</div>
      <div className="text-[11px] text-ink-muted">{note}</div>
    </div>
  );
}

function tokens(value: number): string {
  if (value >= 1_000_000) {
    return `${(value / 1_000_000).toFixed(1)}M`;
  }
  if (value >= 1000) {
    return `${(value / 1000).toFixed(value >= 10_000 ? 0 : 1)}k`;
  }
  return String(value);
}

function percent(value: number | null | undefined): string {
  return value == null ? "–" : `${Math.round(value * 100)}%`;
}

function seconds(ms: number): string {
  return ms >= 10_000 ? `${Math.round(ms / 1000)} s` : `${(ms / 1000).toFixed(1)} s`;
}
