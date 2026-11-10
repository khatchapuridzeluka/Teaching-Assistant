import { useState } from "react";
import { Link } from "react-router";
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { ModelCallItem, ModelCallPurpose, ModelUsageTotal } from "../api/client";
import { FilterChips } from "./FilterChips";
import { useModelUsage } from "../api/hooks";
import { formatDate, formatDay } from "../format";
import { Card, CardHeader, Chip, EmptyState, ErrorPanel, Spinner, Stat, StatStrip } from "./ui";

const series = [
  { key: "uncached", label: "Prompt, not cached", color: "#1857d6" },
  { key: "cached", label: "Prompt, from cache", color: "#a9c1f4" },
  { key: "output", label: "Output", color: "#f2b400" },
] as const;

const purposeLabels: Record<string, string> = {
  Grading: "Single grading",
  GradeAll: "Grade all",
  MistakeAnalysis: "Common mistakes",
  StudentAnalysis: "Student analysis",
  Chat: "Ask agent",
  HealthCheck: "Model check",
};

const periods = [7, 30, 90];

const purposeOptions: { value: ModelCallPurpose | undefined; label: string }[] = [
  { value: undefined, label: "All work" },
  { value: "Grading", label: "Single grading" },
  { value: "GradeAll", label: "Grade all" },
  { value: "MistakeAnalysis", label: "Common mistakes" },
  { value: "StudentAnalysis", label: "Student analysis" },
  { value: "Chat", label: "Ask agent" },
  { value: "HealthCheck", label: "Model check" },
];

export function UsageView() {
  const [days, setDays] = useState(30);
  const [purpose, setPurpose] = useState<ModelCallPurpose | undefined>(undefined);
  const usage = useModelUsage(days, purpose);
  const data = usage.data;
  const summary = data?.summary;
  const workLabel = purpose ? (purposeLabels[purpose] ?? purpose) : "All work";
  const chartData = (data?.days ?? []).map((d) => ({
    date: d.date,
    uncached: d.inputTokens + d.cacheWriteTokens,
    cached: d.cacheReadTokens,
    output: d.outputTokens,
    calls: d.calls,
  }));
  const hasCalls = (data?.totals.length ?? 0) > 0;
  const purposeCounts = new Map((data?.totals ?? []).map((t) => [t.purpose, t.calls]));
  const allCalls = (data?.totals ?? []).reduce((sum, t) => sum + t.calls, 0);

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-3">
        <FilterChips
          label="Work"
          options={purposeOptions.map((o) => ({ ...o, count: o.value ? (purposeCounts.get(o.value) ?? 0) : allCalls }))}
          value={purpose}
          onChange={setPurpose}
        />
        <FilterChips
          label="Period"
          options={periods.map((p) => ({ value: p, label: `${p} days` }))}
          value={days}
          onChange={setDays}
        />
      </div>

      <div className="text-xs text-ink-muted">
        Every Claude call: tokens, cache use, duration and errors · only metadata is recorded, never prompts or answers
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
          <StatStrip columns={5}>
            <Stat
              label={`Calls · ${workLabel}`}
              value={summary?.calls ?? 0}
              note={summary?.failed ? `${summary.failed} failed` : "none failed"}
            />
            <Stat label="Prompt per call" value={summary?.calls ? tokens(summary.averagePromptTokens) : "–"} note="tokens sent, on average" />
            <Stat label="Output per call" value={summary?.calls ? tokens(summary.averageOutputTokens) : "–"} note="tokens written, on average" />
            <Stat label="From cache" value={percent(summary?.cacheHitRate)} note="share of the prompt tokens" />
            <Stat label="Time per call" value={summary?.calls ? seconds(summary.averageDurationMs) : "–"} note="average" />
          </StatStrip>

          <Card>
            <CardHeader title="Tokens per day" subtitle={`${workLabel}, last ${days} days`} />
            <div className="px-[18px] pt-3">
              <Legend />
            </div>
            <div className="h-[240px] px-2 pb-3 pt-1">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData} margin={{ top: 8, right: 12, bottom: 0, left: 4 }} barCategoryGap="20%">
                  <CartesianGrid vertical={false} stroke="#e9ecf3" />
                  <XAxis
                    dataKey="date"
                    tickFormatter={(d: string) => formatDay(d)}
                    tick={{ fontSize: 11, fill: "#5d6985" }}
                    tickLine={false}
                    axisLine={{ stroke: "#dcdfea" }}
                    minTickGap={24}
                  />
                  <YAxis
                    tickFormatter={(v: number) => tokens(v)}
                    tick={{ fontSize: 11, fill: "#5d6985" }}
                    tickLine={false}
                    axisLine={false}
                    width={44}
                  />
                  <Tooltip cursor={{ fill: "rgba(22,32,58,0.06)" }} content={<DayTooltip />} />
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
            <CardHeader title="All work by purpose" subtitle="Always the full picture · prompt tokens include cached ones" />
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
                    <PurposeRow key={t.purpose} total={t} selected={t.purpose === purpose} onSelect={() => setPurpose(t.purpose)} />
                  ))}
                </tbody>
              </table>
            </div>
          </Card>

          <Card>
            <CardHeader title="Recent calls" subtitle={`${workLabel} · newest first · up to 200`} />
            {data.recent.length === 0 ? (
              <EmptyState>No {workLabel.toLowerCase()} calls in the last {days} days.</EmptyState>
            ) : (
              <ul className="@container divide-y divide-line">
                {data.recent.map((call) => (
                  <CallRow key={call.id} call={call} />
                ))}
              </ul>
            )}
          </Card>
        </>
      )}
    </>
  );
}

function PurposeRow({ total, selected, onSelect }: { total: ModelUsageTotal; selected: boolean; onSelect: () => void }) {
  const prompt = total.inputTokens + total.cacheReadTokens + total.cacheWriteTokens;
  return (
    <tr className={`border-b border-line last:border-b-0 ${selected ? "bg-accent-tint/60" : ""}`}>
      <td className="px-[18px] py-2 font-semibold">
        <button onClick={onSelect} className="text-left hover:underline" title="Show only this work">
          {purposeLabels[total.purpose] ?? total.purpose}
        </button>
      </td>
      <td className="px-3 py-2">{total.calls}</td>
      <td className={`px-3 py-2 ${total.failed > 0 ? "font-semibold text-danger" : ""}`}>{total.failed}</td>
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
            <span className="inline-flex h-[22px] items-center rounded-full bg-danger px-2.5 text-[11px] font-semibold text-white">
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
          <div className="mt-0.5 line-clamp-2 text-danger" title={call.error}>
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
