import { useState } from "react";
import { Navigate, useParams, useSearchParams } from "react-router";
import { BookOpen, ClipboardList, Eye, Gavel, LayoutDashboard, NotebookPen, ScrollText } from "lucide-react";
import { useAssignments, useGradingGuidance } from "../api/hooks";
import { CourseName } from "../components/Layout";
import { RubricApproach } from "../components/RubricApproach";
import { RubricAssignments } from "../components/RubricAssignments";
import { BriefModal } from "../components/RubricBrief";
import { RubricGuidance } from "../components/RubricGuidance";
import { RubricOverview } from "../components/RubricOverview";
import { RubricRules } from "../components/RubricRules";
import { rubricPath, type RubricTab } from "../components/RubricShared";
import { Button, ErrorPanel, PageHeader, Spinner, Tabs } from "../components/ui";

const tabs: RubricTab[] = ["overview", "approach", "guidance", "rules", "assignments"];

function parseTab(value: string | null): RubricTab {
  return tabs.find((tab) => tab === value) ?? "overview";
}

export function RubricPolicyPage() {
  const { courseId = "" } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const [briefOpen, setBriefOpen] = useState(false);
  const guidance = useGradingGuidance(courseId);
  const assignments = useAssignments(courseId);
  const tab = parseTab(searchParams.get("tab"));
  const assignmentId = searchParams.get("assignment") ?? undefined;
  const data = guidance.data;
  const list = assignments.data;

  return (
    <>
      <PageHeader
        lead={
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[10px] bg-paper text-ink-muted">
            <ScrollText className="h-5 w-5" />
          </span>
        }
        title="Rubric & policy"
        subtitle={
          <>
            <CourseName courseId={courseId} /> · everything the grader is told before it reads a student's work
          </>
        }
        actions={
          <Button
            icon={<Eye />}
            onClick={() => setBriefOpen(true)}
            disabled={!data || !list}
            title="Read all five layers in the order the grader gets them, for one assignment"
          >
            Preview brief
          </Button>
        }
      />

      <Tabs
        label="Rubric & policy sections"
        value={tab}
        onChange={(next) => setSearchParams(next === "overview" ? {} : { tab: next })}
        tabs={[
          { value: "overview", label: "Overview", icon: <LayoutDashboard /> },
          { value: "approach", label: "Built-in approach", icon: <BookOpen />, count: data?.approach.length },
          { value: "guidance", label: "Course guidance", icon: <NotebookPen /> },
          { value: "rules", label: "Your rules", icon: <Gavel />, count: data?.rules.length },
          { value: "assignments", label: "Assignments", icon: <ClipboardList />, count: list?.length },
        ]}
      />

      {guidance.isError && <ErrorPanel error={guidance.error} />}
      {assignments.isError && <ErrorPanel error={assignments.error} />}
      {(guidance.isLoading || assignments.isLoading) && <Spinner />}

      {data && list && (
        <div key={tab} className="flex animate-fade flex-col gap-4">
          {tab === "overview" && <RubricOverview courseId={courseId} data={data} assignments={list} />}
          {tab === "approach" && <RubricApproach courseId={courseId} data={data} />}
          {tab === "guidance" && <RubricGuidance courseId={courseId} data={data} />}
          {tab === "rules" && <RubricRules courseId={courseId} data={data} />}
          {tab === "assignments" && (
            <RubricAssignments
              courseId={courseId}
              data={data}
              assignments={list}
              selectedId={assignmentId}
              opened={assignmentId !== undefined}
              onSelect={(id) => setSearchParams({ tab: "assignments", assignment: id }, { replace: true })}
            />
          )}
          {briefOpen && (
            <BriefModal courseId={courseId} data={data} assignments={list} initialAssignmentId={assignmentId} onClose={() => setBriefOpen(false)} />
          )}
        </div>
      )}
    </>
  );
}

export function RubricLegacyRedirect() {
  const { courseId = "", "*": rest = "" } = useParams();
  const [layer, assignmentId] = rest.split("/");
  const tab: RubricTab =
    layer === "approach" || layer === "guidance" || layer === "rules" ? layer : layer === "instructions" || layer === "notes" ? "assignments" : "overview";

  return <Navigate to={rubricPath(courseId, tab, assignmentId)} replace />;
}
