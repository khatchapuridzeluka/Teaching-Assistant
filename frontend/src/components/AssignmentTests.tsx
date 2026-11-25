import { type ChangeEvent, useEffect, useState } from "react";
import type { GenerateTests, TestCategory } from "../api/client";
import { useAssignmentJobs, useGenerateTests, useTestSet } from "../api/hooks";
import { FlaskConical, Sparkles, X } from "lucide-react";
import { useTrackJob } from "./JobToasts";
import { categoryInfo, TestSetView } from "./TestSetView";
import { Button, Card, EmptyState, ErrorPanel, Modal, Notice, Segmented, SkeletonCards, Spinner } from "./ui";

const amountOptions: { value: GenerateTests["amount"]; label: string }[] = [
  { value: "AboutThreePerCategory", label: "About 3 per category" },
  { value: "AboutTenInTotal", label: "About 10 in total" },
  { value: "FewestThatCoverEveryRequirement", label: "As few as cover every requirement" },
];

const defaultRequest: GenerateTests = {
  style: "ClassDesign",
  categories: ["Normal", "Boundary", "Invalid"],
  amount: "AboutThreePerCategory",
  focus: "",
  dontTest: "Exact wording of printed messages, class and method names, input prompts.",
  referenceFiles: [],
};

export function AssignmentTests({ courseId, assignmentId, assignmentTitle }: { courseId: string; assignmentId: string; assignmentTitle: string }) {
  const testSet = useTestSet(courseId, assignmentId);
  const jobs = useAssignmentJobs(courseId, assignmentId);
  const generate = useGenerateTests(courseId, assignmentId);
  const trackJob = useTrackJob();
  const [formOpen, setFormOpen] = useState(false);

  const latestJob = (jobs.data ?? []).find((job) => job.type === "GenerateTests");
  const codeJob = (jobs.data ?? []).find((job) => job.type === "GenerateTestCode");
  const running = latestJob && (latestJob.status === "Queued" || latestJob.status === "Running") ? latestJob : undefined;
  const current = testSet.data ?? null;
  const failed =
    latestJob?.status === "Failed" && (!current || new Date(current.createdAt).getTime() < new Date(latestJob.createdAt).getTime())
      ? latestJob
      : undefined;
  const refetchTestSet = testSet.refetch;

  useEffect(() => {
    if (latestJob?.status === "Succeeded") {
      void refetchTestSet();
    }
  }, [latestJob?.id, latestJob?.status, refetchTestSet]);

  useEffect(() => {
    if (codeJob?.status === "Succeeded") {
      void refetchTestSet();
    }
  }, [codeJob?.id, codeJob?.status, refetchTestSet]);

  function submit(request: GenerateTests) {
    generate.mutate(request, {
      onSuccess: (job) => {
        setFormOpen(false);
        trackJob(job, { label: `${assignmentTitle} · test cases`, to: `/courses/${courseId}/assignments/${assignmentId}?view=tests` });
      },
    });
  }

  return (
    <div className="flex flex-col gap-3">
      {running && (
        <Notice>
          <Spinner label="Designing test cases… Claude is reading the instructions and your settings; this usually takes under a minute." />
        </Notice>
      )}
      {failed && (
        <Notice tone="danger" title="Generating test cases failed.">
          {failed.error ?? "Unknown error."}
        </Notice>
      )}

      {testSet.isLoading && <SkeletonCards count={2} />}
      {testSet.isError && <ErrorPanel error={testSet.error} />}

      {testSet.isSuccess && !current && !running && (
        <Card>
          <EmptyState icon={<FlaskConical />}>
            <div className="text-[15px] font-bold text-ink">Check every submission against the same test cases</div>
            <p className="mt-1">
              Claude proposes test cases from the stored instructions and your settings; you review them before they're used. Later, approved
              tests run in the sandbox for every grading and their results go to the grader as evidence.
            </p>
            <Button variant="primary" className="mt-4" icon={<Sparkles />} onClick={() => setFormOpen(true)}>
              Create test cases
            </Button>
          </EmptyState>
        </Card>
      )}

      {current && (
        <TestSetView
          testSet={current}
          courseId={courseId}
          assignmentId={assignmentId}
          busy={!!running}
          codeJob={codeJob}
          jobs={jobs.data ?? []}
          onRegenerate={() => setFormOpen(true)}
        />
      )}

      {formOpen && (
        <GenerateForm
          initial={current?.request ?? defaultRequest}
          regenerating={!!current}
          pending={generate.isPending}
          error={generate.error}
          onSubmit={submit}
          onClose={() => {
            setFormOpen(false);
            generate.reset();
          }}
        />
      )}
    </div>
  );
}

function GenerateForm({
  initial,
  regenerating,
  pending,
  error,
  onSubmit,
  onClose,
}: {
  initial: GenerateTests;
  regenerating: boolean;
  pending: boolean;
  error: unknown;
  onSubmit: (request: GenerateTests) => void;
  onClose: () => void;
}) {
  const [request, setRequest] = useState<GenerateTests>(initial);
  const [problem, setProblem] = useState<string | null>(null);

  function update(change: Partial<GenerateTests>) {
    setRequest((current) => ({ ...current, ...change }));
    setProblem(null);
  }

  function toggle(category: TestCategory) {
    const has = request.categories.includes(category);
    update({ categories: has ? request.categories.filter((c) => c !== category) : [...request.categories, category] });
  }

  async function addFiles(event: ChangeEvent<HTMLInputElement>) {
    const picked = Array.from(event.target.files ?? []);
    event.target.value = "";
    const notJava = picked.find((file) => !file.name.toLowerCase().endsWith(".java"));
    if (notJava) {
      setProblem(`"${notJava.name}" isn't a .java file. Add only the reference solution's .java files.`);
      return;
    }

    const read = await Promise.all(picked.map(async (file) => ({ name: file.name, content: await file.text() })));
    const kept = request.referenceFiles.filter((existing) => !read.some((file) => file.name === existing.name));
    update({ referenceFiles: [...kept, ...read] });
  }

  function submit() {
    if (request.categories.length === 0) {
      setProblem("Choose at least one kind of test case to cover.");
      return;
    }

    onSubmit({ ...request, focus: request.focus?.trim() || null, dontTest: request.dontTest?.trim() || null });
  }

  return (
    <Modal
      title={regenerating ? "Generate test cases again" : "Create test cases"}
      subtitle={regenerating ? "The result is a new draft version; the current one stays in the history." : undefined}
      onClose={onClose}
      busy={pending}
      footer={
        <div className="flex w-full items-center justify-between gap-3">
          <span className="text-xs text-ink-muted">One paid AI call. It runs in the background; you can leave this page.</span>
          <div className="flex gap-2">
            <Button onClick={onClose} disabled={pending}>
              Cancel
            </Button>
            <Button variant="primary" icon={<Sparkles />} onClick={submit} disabled={pending}>
              {pending ? "Queuing…" : "Generate test cases"}
            </Button>
          </div>
        </div>
      }
    >
      <div className="flex flex-col gap-4 px-5 py-4">
        <section>
          <div className="mb-1.5 font-semibold">Test style</div>
          <Segmented
            label="Test style"
            value={request.style}
            onChange={(style) => update({ style })}
            options={[
              { value: "Console", label: "Console: input → output" },
              { value: "ClassDesign", label: "Class design: neutral interface" },
            ]}
          />
          <p className="mt-1 text-xs text-ink-muted">
            {request.style === "Console"
              ? "The program's main is run with each case's input and its printed results are compared, so names never matter."
              : "Tests call a neutral interface; each student's classes are connected to it by a small adapter, so their own class and method names don't matter."}
          </p>
        </section>

        <section>
          <div className="mb-1.5 font-semibold">What to cover</div>
          <div className="flex flex-wrap gap-2">
            {(Object.keys(categoryInfo) as TestCategory[]).map((category) => {
              const on = request.categories.includes(category);
              return (
                <label
                  key={category}
                  className={`flex cursor-pointer items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-[13px] ${
                    on ? "border-accent bg-accent-tint font-semibold text-accent-strong" : "border-line text-ink-soft"
                  }`}
                >
                  <input type="checkbox" checked={on} onChange={() => toggle(category)} className="accent-accent" />
                  {categoryInfo[category].formLabel}
                </label>
              );
            })}
          </div>
        </section>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <section>
            <div className="mb-1.5 font-semibold">How many</div>
            <select
              value={request.amount}
              onChange={(e) => update({ amount: e.target.value as GenerateTests["amount"] })}
              className="h-[34px] w-full rounded-[7px] border border-line bg-white px-2.5 text-[13px]"
            >
              {amountOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </section>
          <section>
            <div className="mb-1.5 font-semibold">
              Reference solution <span className="font-normal text-ink-faint">optional</span>
            </div>
            <label className="flex h-[34px] cursor-pointer items-center justify-center rounded-[7px] border border-dashed border-ink-faint px-2.5 text-[13px] text-ink-muted hover:bg-paper">
              Add .java files
              <input type="file" accept=".java" multiple onChange={addFiles} className="hidden" />
            </label>
          </section>
        </div>

        {request.referenceFiles.length > 0 && (
          <ul className="-mt-2 flex flex-wrap gap-1.5">
            {request.referenceFiles.map((file) => (
              <li key={file.name} className="flex items-center gap-1 rounded-md border border-line bg-paper px-2 py-0.5 font-mono text-xs">
                {file.name}
                <button
                  aria-label={`Remove ${file.name}`}
                  onClick={() => update({ referenceFiles: request.referenceFiles.filter((f) => f.name !== file.name) })}
                  className="rounded p-0.5 text-ink-faint hover:bg-paper-dark hover:text-ink"
                >
                  <X className="h-3 w-3" />
                </button>
              </li>
            ))}
          </ul>
        )}

        <section>
          <div className="mb-1.5 font-semibold">Focus</div>
          <textarea
            value={request.focus ?? ""}
            onChange={(e) => update({ focus: e.target.value })}
            placeholder="Withdraw must never make the balance negative."
            className="h-[64px] w-full rounded-[7px] border border-line px-2.5 py-2 text-[13px]"
          />
        </section>

        <section>
          <div className="mb-1.5 font-semibold">Don't test</div>
          <textarea
            value={request.dontTest ?? ""}
            onChange={(e) => update({ dontTest: e.target.value })}
            className="h-[52px] w-full rounded-[7px] border border-line px-2.5 py-2 text-[13px]"
          />
        </section>

        {problem && <div className="text-[13px] text-danger-ink">{problem}</div>}
        {error != null && <ErrorPanel error={error} />}
      </div>
    </Modal>
  );
}
