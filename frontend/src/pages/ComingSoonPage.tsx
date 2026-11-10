import { CircleDashed } from "lucide-react";
import { Card, Chip, PageHeader } from "../components/ui";
import type { Section } from "../navigation";

export function ComingSoonPage({ section }: { section: Section }) {
  return (
    <>
      <PageHeader
        lead={
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[10px] border border-dashed border-ink-faint text-ink-muted">
            <section.icon className="h-5 w-5" />
          </span>
        }
        title={section.label}
        subtitle={section.description}
        actions={<Chip tone="dashed">Planned</Chip>}
      />
      <Card className="max-w-[760px] px-[18px] py-4">
        <div className="eyebrow mb-3">What it will show</div>
        <ul className="flex flex-col gap-2.5">
          {(section.plans ?? []).map((plan) => (
            <li key={plan} className="flex items-start gap-2.5">
              <CircleDashed className="mt-0.5 h-4 w-4 shrink-0 text-ink-faint" />
              <span>{plan}</span>
            </li>
          ))}
        </ul>
        <p className="mt-4 border-t border-paper-dark pt-3 text-xs text-ink-muted">
          Not available yet. Grading drafts are always reviewed and approved by you.
        </p>
      </Card>
    </>
  );
}
