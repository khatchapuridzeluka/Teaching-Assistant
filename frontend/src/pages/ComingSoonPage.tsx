import { Card, Chip, PageHeader } from "../components/ui";
import type { Section } from "../navigation";

/** A section that is planned but not built yet: what it will do, so the navigation already shows the whole product. */
export function ComingSoonPage({ section }: { section: Section }) {
  return (
    <>
      <PageHeader title={section.label} subtitle={section.description} actions={<Chip tone="dashed">Planned</Chip>} />
      <Card className="px-[18px] py-4">
        <div className="mb-2 text-[11px] font-bold uppercase tracking-[0.06em] text-accent">What it will show</div>
        <ul className="flex list-disc flex-col gap-1.5 pl-5">
          {(section.plans ?? []).map((plan) => (
            <li key={plan}>{plan}</li>
          ))}
        </ul>
        <p className="mt-4 text-xs text-ink-muted">Not available yet. Grading drafts are always reviewed and approved by you.</p>
      </Card>
    </>
  );
}
