import { useState } from "react";
import { useConnectGoogle, useDisconnectGoogle, useGoogleStatus, useModelHealth } from "../api/hooks";
import type { GoogleStatus } from "../api/client";
import { ConfirmDialog } from "../components/ConfirmDialog";
import { Cpu, Lock, Plug } from "lucide-react";
import { Button, Card, CardHeader, Chip, type ChipTone, ErrorPanel, PageHeader, Spinner } from "../components/ui";

export function SettingsPage() {
  return (
    <>
      <PageHeader title="Settings" subtitle="Connections: where submissions come from and which model drafts the grades." />
      <div className="grid grid-cols-1 gap-[18px] lg:grid-cols-2">
        <GoogleConnection />
        <ModelHealth />
      </div>
    </>
  );
}

const stateChip: Record<GoogleStatus["state"], { tone: ChipTone; label: string }> = {
  Connected: { tone: "success", label: "Connected" },
  NotConnected: { tone: "warn", label: "Not connected" },
  ReconnectRequired: { tone: "warn", label: "Reconnect required" },
  NotConfigured: { tone: "muted", label: "Not configured" },
};

const scopeNames: Record<string, string> = {
  "https://www.googleapis.com/auth/classroom.courses.readonly": "See your Classroom courses",
  "https://www.googleapis.com/auth/classroom.coursework.students.readonly": "See coursework and student submissions",
  "https://www.googleapis.com/auth/classroom.rosters.readonly": "See class rosters",
  "https://www.googleapis.com/auth/classroom.profile.photos": "See students' profile photos",
  "https://www.googleapis.com/auth/drive.readonly": "Download submitted Drive files",
};

function GoogleConnection() {
  const status = useGoogleStatus();
  const connect = useConnectGoogle();
  const disconnect = useDisconnectGoogle();
  const [confirmDisconnect, setConfirmDisconnect] = useState(false);
  const state = status.data?.state;

  return (
    <Card>
      <CardHeader
        icon={<Plug />}
        title="Google Classroom"
        subtitle="Read-only access: the app can never publish grades."
        actions={state && <Chip tone={stateChip[state].tone}>{stateChip[state].label}</Chip>}
      />
      <div className="flex flex-col gap-3 px-[18px] py-3.5">
        {status.isLoading && <Spinner />}
        {status.isError && <ErrorPanel error={status.error} />}
        {status.data?.message && <div className="text-ink-muted">{status.data.message}</div>}

        {status.data && status.data.grantedScopes.length > 0 && (
          <ul className="flex flex-col">
            {status.data.grantedScopes.map((scope) => (
              <li key={scope} className="flex items-center justify-between border-b border-paper-dark py-1.5 text-xs last:border-b-0">
                <span className="flex items-center gap-2">
                  <Lock className="h-3.5 w-3.5 text-ink-faint" />
                  {scopeNames[scope] ?? scope}
                </span>
                <Chip tone="muted">read-only</Chip>
              </li>
            ))}
          </ul>
        )}

        {connect.isPending && <Spinner label="Waiting for you to approve in the browser window that opened…" />}
        {connect.isError && <ErrorPanel error={connect.error} />}
        {disconnect.isError && <ErrorPanel error={disconnect.error} />}

        <div className="flex gap-2">
          {state !== "Connected" && (
            <Button variant="primary" small onClick={() => connect.mutate()} disabled={connect.isPending || state === "NotConfigured"}>
              {state === "ReconnectRequired" ? "Reconnect" : "Connect"}
            </Button>
          )}
          {state === "Connected" && (
            <Button
              small
              disabled={disconnect.isPending}
              onClick={() => setConfirmDisconnect(true)}
            >
              Disconnect
            </Button>
          )}
          {confirmDisconnect && (
            <ConfirmDialog
              title="Disconnect Google Classroom?"
              confirmLabel="Disconnect"
              danger
              onConfirm={() => {
                setConfirmDisconnect(false);
                disconnect.mutate();
              }}
              onCancel={() => setConfirmDisconnect(false)}
            >
              <p>
                This revokes the app's read-only access to your Google Classroom. Stored submissions, drafts and reports stay; you
                can connect again at any time.
              </p>
            </ConfirmDialog>
          )}
        </div>
      </div>
    </Card>
  );
}

function ModelHealth() {
  const check = useModelHealth();
  const result = check.data;

  return (
    <Card>
      <CardHeader
        icon={<Cpu />}
        title="Grading model"
        subtitle="Sends one short test prompt (uses a few tokens)."
        actions={result && <Chip tone={result.healthy ? "success" : "danger"}>{result.healthy ? `Healthy · ${result.latencyMs} ms` : "Unavailable"}</Chip>}
      />
      <div className="flex flex-col gap-3 px-[18px] py-3.5">
        {result && (
          <div className="flex flex-col text-xs">
            <div className="flex justify-between py-1"><span>Model</span><b className="font-mono">{result.model}</b></div>
            <div className="flex justify-between py-1"><span>Reply</span><b>{result.reply ?? "–"}</b></div>
            {result.error && <div className="pt-1 text-ink-muted">{result.error}</div>}
          </div>
        )}
        {check.isError && <ErrorPanel error={check.error} />}
        <div>
          <Button small onClick={() => check.mutate()} disabled={check.isPending}>
            {check.isPending ? "Testing…" : "Test connection"}
          </Button>
        </div>
      </div>
    </Card>
  );
}
