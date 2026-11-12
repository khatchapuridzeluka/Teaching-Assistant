import { FileDown } from "lucide-react";
import { buttonClass } from "./ui";

export function ExportPdfLink({ gradingId }: { gradingId: string }) {
  return (
    <a
      href={`/api/gradings/${gradingId}/export/pdf`}
      download
      className={buttonClass("ghost", true)}
      title="Download this grading as a PDF report: your report when you saved one, otherwise the AI draft, marked as not reviewed until you approve it"
    >
      <FileDown className="h-3.5 w-3.5" /> PDF
    </a>
  );
}
