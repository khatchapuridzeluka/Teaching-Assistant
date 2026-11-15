import { useState } from "react";
import { NotebookPen, Pencil } from "lucide-react";
import type { GradingGuidance } from "../api/client";
import { useSetCourseGuidance } from "../api/hooks";
import { formatDate } from "../format";
import { CourseName } from "./Layout";
import { Bullet, LayerAside } from "./RubricShared";
import { Button, Card, CardHeader, ErrorPanel, Meter, Modal } from "./ui";

const guidanceLimit = 4000;

export function RubricGuidance({ courseId, data }: { courseId: string; data: GradingGuidance }) {
  const [editing, setEditing] = useState(false);
  const text = data.courseGuidance ?? "";

  return (
    <>
      <div className="@container/page">
        <div className="grid items-start gap-4 @4xl/page:grid-cols-[minmax(0,1fr)_300px]">
          {text ? (
            <Card className="overflow-hidden">
              <CardHeader
                icon={<NotebookPen />}
                title="How to grade this course"
                subtitle={`In use from the next grading · saved ${formatDate(data.courseGuidanceUpdatedAt)}`}
                actions={
                  <Button small icon={<Pencil />} onClick={() => setEditing(true)}>
                    Edit
                  </Button>
                }
              />
              <p className="max-w-[78ch] whitespace-pre-line px-[18px] py-4 text-[13.5px] leading-[1.7]">{text}</p>
              <div className="flex flex-wrap items-center gap-3 border-t border-line bg-paper/50 px-[18px] py-2.5 text-[11px] text-ink-muted">
                <Meter percent={(text.length / guidanceLimit) * 100} className="w-24" />
                <span>
                  <b className="num text-ink-soft">{text.length.toLocaleString()}</b> of {guidanceLimit.toLocaleString()} characters
                </span>
                <span className="ml-auto">
                  Only <CourseName courseId={courseId} /> is graded with this text
                </span>
              </div>
            </Card>
          ) : (
            <Card className="overflow-hidden">
              <CardHeader
                icon={<NotebookPen />}
                title="Not written yet"
                subtitle="This course is graded with the built-in approach and your rules only"
                actions={
                  <Button small variant="primary" icon={<Pencil />} onClick={() => setEditing(true)}>
                    Write guidance
                  </Button>
                }
              />
              <div className="flex flex-col gap-3 px-[18px] py-4">
                <div className="rounded-lg border border-dashed border-ink-faint bg-paper/60 px-3.5 py-3 text-[13px] leading-relaxed text-ink-muted">
                  <div className="eyebrow mb-1">For example</div>
                  “These are second-year students. Judge mainly whether the program works and the classes are designed sensibly. Don’t be
                  strict about formatting; mention it as a suggestion. Be encouraging in the feedback.”
                </div>
                <p className="text-xs text-ink-muted">
                  One paragraph is enough. It sets the tone for this group, for example more lenient for C++ basics than for Advanced Java.
                </p>
              </div>
            </Card>
          )}

          <LayerAside courseId={courseId} layerKey="guidance">
            <p>
              Anything true for every assignment of this course. The grader reads it after the built-in approach and before your rules;
              your rules still decide the points.
            </p>
            <div>
              <div className="eyebrow mb-1.5">What to write here</div>
              <ul className="flex flex-col gap-2 text-[13px]">
                <Bullet title="Level of the group">what they already know, their age or year</Bullet>
                <Bullet title="How strict to be">what costs points and what is only a suggestion</Bullet>
                <Bullet title="What matters most">working code, class design, best practices</Bullet>
                <Bullet title="Feedback tone">how to speak to this group</Bullet>
              </ul>
            </div>
          </LayerAside>
        </div>
      </div>

      {editing && <GuidanceModal courseId={courseId} text={text} onClose={() => setEditing(false)} />}
    </>
  );
}

function GuidanceModal({ courseId, text, onClose }: { courseId: string; text: string; onClose: () => void }) {
  const [value, setValue] = useState(text);
  const save = useSetCourseGuidance(courseId);
  const removing = value.trim().length === 0 && text.length > 0;

  return (
    <Modal
      title={text ? "Edit course guidance" : "Write course guidance"}
      subtitle={<CourseName courseId={courseId} />}
      onClose={onClose}
      busy={save.isPending}
      size="max-w-2xl"
      footer={
        <>
          <Button onClick={onClose} disabled={save.isPending}>
            Cancel
          </Button>
          <Button variant={removing ? "danger" : "primary"} onClick={() => save.mutate(value.trim(), { onSuccess: onClose })} disabled={save.isPending}>
            {save.isPending ? "Saving…" : removing ? "Remove guidance" : "Save"}
          </Button>
        </>
      }
    >
      <div className="flex shrink-0 flex-col gap-1.5 px-5 py-4">
        <label htmlFor="course-guidance" className="eyebrow">
          How to grade this course
        </label>
        <textarea
          id="course-guidance"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          maxLength={guidanceLimit}
          rows={14}
          autoFocus
          placeholder="These are second-year students. Judge mainly whether the program works and the classes are designed sensibly…"
          className="field min-h-[300px] resize-y py-2 leading-relaxed"
        />
        <span className="text-[11px] text-ink-muted">
          {value.trim().length} / {guidanceLimit} characters · applies to the next grading in this course
          {removing ? " · empty removes the guidance" : ""}
        </span>
        {save.isError && <ErrorPanel error={save.error} />}
      </div>
    </Modal>
  );
}
