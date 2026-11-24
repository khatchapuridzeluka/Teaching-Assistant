import { useState } from "react";
import type { JobInfo, TestCode } from "../api/client";
import { useGenerateTestCode } from "../api/hooks";
import { formatDate } from "../format";
import { Code2, RefreshCw } from "lucide-react";
import { Button, Card, CardHeader, Chip, ErrorPanel, Notice, Segmented, Spinner } from "./ui";

type File = "tests" | "placeholder";

export function TestCodeCard({
  code,
  job,
  testSetId,
  courseId,
  assignmentId,
}: {
  code: TestCode | null;
  job: JobInfo | undefined;
  testSetId: string;
  courseId: string;
  assignmentId: string;
}) {
  const generate = useGenerateTestCode(courseId, assignmentId);
  const [file, setFile] = useState<File>("tests");
  const running = job && (job.status === "Queued" || job.status === "Running");
  const failed = job?.status === "Failed" && (!code || new Date(code.createdAt).getTime() < new Date(job.createdAt).getTime()) ? job : undefined;

  const writeButton = (label: string) => (
    <Button small icon={<RefreshCw />} disabled={!!running || generate.isPending} onClick={() => generate.mutate(testSetId)}>
      {generate.isPending ? "Queuing…" : label}
    </Button>
  );

  return (
    <Card>
      <CardHeader
        icon={<Code2 />}
        title="Test code"
        subtitle="JUnit written from the approved cases; every student's code runs against the same class"
        actions={code && !running ? writeButton("Write again") : undefined}
      />
      <div className="flex flex-col gap-3 px-[18px] py-3">
        {running && <Spinner label="Writing the test code and compiling it in the sandbox… this usually takes under a minute." />}
        {failed && (
          <Notice tone="danger" title="Writing the test code failed." action={writeButton("Try again")}>
            {failed.error ?? "Unknown error."}
          </Notice>
        )}
        {generate.isError && <ErrorPanel error={generate.error} />}

        {!code && !running && !failed && (
          <div className="flex items-center justify-between gap-3 text-ink-muted">
            <span>No test code yet. It's written when you approve; one paid AI call.</span>
            {writeButton("Write test code")}
          </div>
        )}

        {code && (
          <>
            <div className="flex flex-wrap items-center gap-2">
              {code.status === "Compiled" ? (
                <Chip tone="success">Compiles</Chip>
              ) : code.status === "CompileFailed" ? (
                <Chip tone="danger">Doesn't compile</Chip>
              ) : (
                <Chip tone="warn">Not compiled</Chip>
              )}
              <span className="text-xs text-ink-muted">
                Written {formatDate(code.createdAt)} by {code.model}, compiled with {code.interfaceFile} and a placeholder adapter
              </span>
            </div>
            {code.compileOutput && (
              <pre className="max-h-60 overflow-auto whitespace-pre-wrap rounded-md border border-line bg-paper px-3 py-2 font-mono text-[11px] leading-relaxed text-ink-soft">
                {code.compileOutput}
              </pre>
            )}
            <Segmented
              label="File"
              value={file}
              onChange={setFile}
              options={[
                { value: "tests", label: "AssignmentTests.java" },
                { value: "placeholder", label: "StudentAdapter.java (placeholder)" },
              ]}
            />
            <pre className="max-h-[520px] overflow-auto rounded-md border border-line bg-paper px-3 py-2 font-mono text-[11.5px] leading-relaxed text-ink-soft">
              {file === "tests" ? code.testClass : code.stubAdapter}
            </pre>
            {file === "placeholder" && (
              <p className="text-xs text-ink-muted">
                Only used to check that the tests compile. Each student gets their own StudentAdapter that connects these methods to their classes.
              </p>
            )}
          </>
        )}
      </div>
    </Card>
  );
}
