import { useConnectGoogle, useDisconnectGoogle, useGoogleStatus, useModelHealth } from "../api/hooks";
import type { GoogleStatus } from "../api/client";
import { Button, Card, CardHeader, Chip, type ChipTone, ErrorPanel, PageHeader, Spinner } from "../components/ui";

// GET /auth/google/status, POST /auth/google/connect, POST /auth/google/disconnect, GET /health/model
export function SettingsPage() {
  return (
    <>
      <PageHeader title="Connections" subtitle="Where submissions come from and which model drafts the grades." />
      <div className="grid grid-cols-1 gap-[18px] lg:grid-cols-2">
        <GoogleConnection />
        <ModelHealth />
      </div>
    </>
  );
}

const stateChip: Record<GoogleStatus["state"], { tone: ChipTone; label: string }> = {
  Connected: { tone: "accent", label: "Connected" },
  NotConnected: { tone: "outline", label: "Not connected" },
  ReconnectRequired: { tone: "ink", label: "Reconnect required" },
  NotConfigured: { tone: "muted", label: "Not configured" },
};

// Plain-language names for the read-only scopes.
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
  const state = status.data?.state;

  return (
    <Card>
      <CardHeader
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
                <span>{scopeNames[scope] ?? scope}</span>
                <Chip tone="tint">read-only</Chip>
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
              onClick={() => confirm("Revoke the app's access to Google Classroom?") && disconnect.mutate()}
            >
              Disconnect
            </Button>
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
        title="Grading model"
        subtitle="Sends one short test prompt (uses a few tokens)."
        actions={result && <Chip tone={result.healthy ? "accent" : "ink"}>{result.healthy ? `Healthy · ${result.latencyMs} ms` : "Unavailable"}</Chip>}
      />
      <div className="flex flex-col gap-3 px-[18px] py-3.5">
        {result && (
          <div className="flex flex-col text-xs">
            <div className="flex justify-between py-1"><span>Model</span><b>{result.model}</b></div>
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
