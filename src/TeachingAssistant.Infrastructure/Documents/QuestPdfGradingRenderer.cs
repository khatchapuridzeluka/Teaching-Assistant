using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TeachingAssistant.Application.Interfaces;
using TeachingAssistant.Application.Models;
using TeachingAssistant.Domain.Grading;

namespace TeachingAssistant.Infrastructure.Documents;

public partial class QuestPdfGradingRenderer : IGradingDocumentRenderer
{
    private const string Ink = "#16203a";
    private const string InkSoft = "#2b3757";
    private const string Muted = "#5d6985";
    private const string Faint = "#8f99b1";
    private const string Navy = "#0f1a36";
    private const string Line = "#dcdfea";
    private const string Paper = "#f4f5f9";
    private const string PaperDark = "#e9ecf3";
    private const string Accent = "#1857d6";
    private const string AccentStrong = "#1146b4";
    private const string AccentTint = "#e7eeff";
    private const string Danger = "#cf3a2b";
    private const string DangerTint = "#ffe7e2";
    private const string DangerInk = "#9c2318";
    private const string Warn = "#e0921a";
    private const string WarnTint = "#ffefcc";
    private const string WarnInk = "#7a4a00";
    private const string StyleGray = "#8391ad";
    private const string Success = "#1c8a4e";
    private const string SuccessTint = "#dcf5e6";
    private const string SuccessInk = "#0f5d33";

    private static readonly string[] Sans = ["FiraGO", "Noto Sans Georgian"];
    private static readonly string[] Mono = ["JetBrains Mono", "FiraGO", "Noto Sans Georgian"];
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UsePipeTables().Build();

    private const string LogoMark =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32"><rect width="32" height="32" rx="8" fill="#1857d6"/><path d="M10.5 9.5 5.8 16l4.7 6.5" fill="none" stroke="#ffffff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/><path d="M21.5 9.5 26.2 16l-4.7 6.5" fill="none" stroke="#ffffff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/><path d="m12.6 16.4 2.5 2.6 4.5-5.6" fill="none" stroke="#bcd0ff" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

    private const string ThumbsUp =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path d="M7 10v12M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2a3.13 3.13 0 0 1 3 3.88Z" fill="none" stroke="#0f5d33" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

    private const string CircleCheck =
        """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="none" stroke="#0f5d33" stroke-width="2"/><path d="m9 12 2 2 4-4" fill="none" stroke="#0f5d33" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>""";

    static QuestPdfGradingRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

        var assembly = typeof(QuestPdfGradingRenderer).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            FontManager.RegisterFontFromStream(stream);
        }
    }

    public byte[] RenderPdf(GradingDocument document)
    {
        return Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(40);
                    page.MarginVertical(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(s => s.FontSize(9.5f).FontColor(Ink).FontFamily(Sans).LineHeight(1.4f));
                    page.Header().Element(c => Header(c, document));
                    page.Content().PaddingTop(14).Element(c => Body(c, document));
                    page.Footer().Element(c => Footer(c, document));
                });
            })
            .WithMetadata(new DocumentMetadata
            {
                Title = $"{document.AssignmentTitle} - {document.StudentName}",
                Author = "Teaching Assistant",
                Subject = "Grading report",
            })
            .GeneratePdf();
    }

    private static void Header(IContainer container, GradingDocument document)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.AutoItem().AlignMiddle().Width(16).Height(16).Svg(LogoMark);
                row.AutoItem().AlignMiddle().PaddingLeft(7).Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(8.5f).FontColor(Muted));
                    text.Span("Teaching Assistant").SemiBold().FontColor(Ink);
                    text.Span("  /  ").FontColor(Faint);
                    text.Span(document.AssignmentTitle);
                    text.Span("  /  ").FontColor(Faint);
                    text.Span(document.StudentName).SemiBold().FontColor(InkSoft);
                });
                row.RelativeItem().AlignRight().AlignMiddle().Text($"Generated {Date(document.GeneratedAt)}").FontSize(8).FontColor(Faint);
            });
            column.Item().PaddingTop(8).LineHorizontal(0.75f).LineColor(Line);
        });
    }

    private static void Footer(IContainer container, GradingDocument document)
    {
        var note = document.Status == GradingDocumentStatus.Approved
            ? $"Prepared with AI assistance; reviewed and approved by the teacher{(document.ApprovedAt is { } at ? $" on {Date(at)}" : "")}."
            : "Prepared with AI assistance; not yet approved by the teacher.";

        container.Column(column =>
        {
            column.Item().LineHorizontal(0.75f).LineColor(Line);
            column.Item().PaddingTop(5).Row(row =>
            {
                row.RelativeItem().Text(note).FontSize(7.5f).FontColor(Faint);
                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Faint));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
    }

    private static void Body(IContainer container, GradingDocument document)
    {
        container.Column(column =>
        {
            column.Item().Text($"GRADING REPORT  ·  VERSION {document.VersionNumber}")
                .FontSize(7.5f).SemiBold().LetterSpacing(0.08f).FontColor(Muted);
            column.Item().PaddingTop(3).Text(document.AssignmentTitle).FontSize(20).Bold().FontColor(Navy).LineHeight(1.15f);
            if (document.Status != GradingDocumentStatus.Approved)
            {
                column.Item().PaddingTop(10).Element(c => Notice(c, document));
            }

            column.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem().PaddingRight(16).Column(main => Main(main, document));
                row.ConstantItem(168).Column(side => Details(side, document));
            });
        });
    }

    private static void Notice(IContainer container, GradingDocument document)
    {
        var (background, border, ink, text) = document.Status switch
        {
            GradingDocumentStatus.InReview => (AccentTint, Accent, AccentStrong,
                "In review: the teacher is still checking this report, so the grade may change."),
            GradingDocumentStatus.Discarded => (Paper, StyleGray, InkSoft,
                "Discarded: this draft was thrown away and is not the current grading."),
            _ => (WarnTint, Warn, WarnInk,
                "AI draft, not reviewed: written by the AI and not yet checked by the teacher; the grade and comments may change."),
        };

        container.Background(background).BorderLeft(3).BorderColor(border).CornerRadius(6)
            .PaddingVertical(7).PaddingHorizontal(10).Text(text).FontSize(9).FontColor(ink);
    }

    private static void Main(ColumnDescriptor column, GradingDocument document)
    {
        if (!string.IsNullOrWhiteSpace(document.Review))
        {
            column.Item().Element(c => Eyebrow(c, "Review"));
            column.Item().Element(c => Markdown(c, document.Review));
        }

        var mistakes = document.Mistakes.OrderBy(m => m.Severity)
            .Select(m => (Action<IContainer>)(c => MistakeCard(c, m)))
            .DefaultIfEmpty(NoMistakes)
            .ToList();
        Section(column, $"Mistakes  ·  {document.Mistakes.Count}", mistakes);

        if (document.Strengths.Count > 0)
        {
            var strengths = document.Strengths.Select(s => (Action<IContainer>)(c => StrengthCard(c, s))).ToList();
            Section(column, $"Strengths  ·  {document.Strengths.Count}", strengths);
        }
    }

    private static void Details(ColumnDescriptor column, GradingDocument document)
    {
        column.Item().Border(0.75f).BorderColor(Line).CornerRadius(6).Column(panel =>
        {
            panel.Item().Background(Paper).BorderBottom(0.75f).BorderColor(Line).PaddingVertical(6).PaddingHorizontal(10)
                .Text("Details").FontSize(9).SemiBold().FontColor(InkSoft);
            panel.Item().Padding(10).Column(fields =>
            {
                fields.Spacing(9);
                Field(fields, "Status", c => StatusChip(c, document.Status));
                Field(fields, "Student", c => c.Row(row =>
                {
                    row.AutoItem().AlignMiddle().Element(a => Avatar(a, document.StudentName));
                    row.RelativeItem().PaddingLeft(6).AlignMiddle().Text(document.StudentName).SemiBold();
                }));
                Field(fields, "Score", c => c.Column(score =>
                {
                    score.Item().Text(text =>
                    {
                        text.Span(document.Score is { } value ? Number(value) : "–").FontSize(22).Bold().FontColor(Navy);
                        if (document.MaxScore is { } max)
                        {
                            text.Span($" / {Number(max)}").FontSize(11).FontColor(Muted);
                        }
                    });
                    if (document.Bonus is > 0)
                    {
                        score.Item().Text($"+{Number(document.Bonus.Value)} bonus").SemiBold().FontColor(Success);
                    }
                }));

                var aiDiffers = document.Status != GradingDocumentStatus.Draft
                    && (document.AiScore != document.Score || (document.AiBonus ?? 0) != (document.Bonus ?? 0));
                if (aiDiffers && document.AiScore is { } ai)
                {
                    var aiBonus = document.AiBonus is > 0 ? $" +{Number(document.AiBonus.Value)}" : "";
                    Field(fields, "AI suggested", c => c.Text($"{Number(ai)}{aiBonus}").FontColor(InkSoft));
                }

                Field(fields, "Mistakes", c => c.Column(counts =>
                {
                    counts.Spacing(2);
                    CountLine(counts, Danger, "Major", document.Mistakes.Count(m => m.Severity == MistakeSeverity.Major));
                    CountLine(counts, Warn, "Minor", document.Mistakes.Count(m => m.Severity == MistakeSeverity.Minor));
                    CountLine(counts, StyleGray, "Style", document.Mistakes.Count(m => m.Severity == MistakeSeverity.Style));
                }));

                var deducted = document.Mistakes.Sum(m => m.Points ?? 0);
                Field(fields, "Points deducted", c =>
                {
                    if (deducted > 0)
                    {
                        PointsPill(c, deducted);
                    }
                    else
                    {
                        c.Text("0").SemiBold().FontColor(InkSoft);
                    }
                });
                Field(fields, "Strengths", c => c.Row(row =>
                {
                    row.AutoItem().AlignMiddle().Width(10).Height(10).Svg(ThumbsUp);
                    row.RelativeItem().PaddingLeft(5).AlignMiddle().Text(document.Strengths.Count.ToString(CultureInfo.InvariantCulture));
                }));
            });

            panel.Item().BorderTop(0.75f).BorderColor(Line).Padding(10).Column(dates =>
            {
                dates.Spacing(3);
                DateLine(dates, "Turned in", document.TurnedInAt);
                DateLine(dates, "Due", document.DueAt);
                DateLine(dates, "Graded", document.GradedAt);
                DateLine(dates, "Approved", document.ApprovedAt);
            });
        });
    }

    private static void Field(ColumnDescriptor fields, string label, Action<IContainer> value)
    {
        fields.Item().Column(field =>
        {
            field.Item().Text(label).FontSize(7.5f).SemiBold().FontColor(Muted);
            field.Item().PaddingTop(2).Element(value);
        });
    }

    private static void CountLine(ColumnDescriptor counts, string color, string label, int count)
    {
        counts.Item().Row(row =>
        {
            row.AutoItem().AlignMiddle().Width(7).Height(7).Background(color).CornerRadius(3.5f);
            row.RelativeItem().PaddingLeft(6).AlignMiddle().Text(label).FontSize(9).SemiBold().FontColor(color);
            row.AutoItem().AlignMiddle().Text(count.ToString(CultureInfo.InvariantCulture)).FontSize(9).SemiBold();
        });
    }

    private static void DateLine(ColumnDescriptor dates, string label, DateTimeOffset? value)
    {
        if (value is not { } date)
        {
            return;
        }

        dates.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontSize(8).FontColor(Muted);
            row.AutoItem().Text(Date(date)).FontSize(8).FontColor(InkSoft);
        });
    }

    private static void MistakeCard(IContainer container, GradingDocumentMistake mistake)
    {
        var color = mistake.Severity switch
        {
            MistakeSeverity.Major => Danger,
            MistakeSeverity.Minor => Warn,
            _ => StyleGray,
        };

        Card(container, color, header =>
        {
            header.AutoItem().AlignMiddle().Text(mistake.Severity.ToString()).FontSize(8.5f).Bold().FontColor(color);
            header.AutoItem().PaddingLeft(6).AlignMiddle().Text(Category(mistake.Category)).FontSize(8.5f).FontColor(Muted);
            if (mistake.Points is > 0)
            {
                header.AutoItem().PaddingLeft(6).AlignMiddle().Element(c => PointsPill(c, mistake.Points.Value));
            }

            header.RelativeItem().AlignRight().AlignMiddle()
                .Text(Location(mistake.File, mistake.StartLine, mistake.EndLine) ?? "").FontSize(7.5f).FontFamily(Mono).FontColor(Faint);
        }, mistake.Message);
    }

    private static void StrengthCard(IContainer container, GradingDocumentStrength strength)
    {
        Card(container, Success, header =>
        {
            header.AutoItem().AlignMiddle().Width(9).Height(9).Svg(ThumbsUp);
            header.AutoItem().PaddingLeft(4).AlignMiddle().Text("Strength").FontSize(8.5f).Bold().FontColor(SuccessInk);
            header.AutoItem().PaddingLeft(6).AlignMiddle().Text(Category(strength.Category)).FontSize(8.5f).FontColor(Muted);
            header.RelativeItem().AlignRight().AlignMiddle()
                .Text(Location(strength.File, strength.StartLine, strength.EndLine) ?? "").FontSize(7.5f).FontFamily(Mono).FontColor(Faint);
        }, strength.Message);
    }

    private static void Card(IContainer container, string accent, Action<RowDescriptor> header, string message)
    {
        container.PaddingBottom(5).Border(0.75f).BorderColor(Line).CornerRadius(6).Row(row =>
        {
            row.ConstantItem(3).CornerRadiusTopLeft(6).CornerRadiusBottomLeft(6).Background(accent);
            row.RelativeItem().PaddingVertical(7).PaddingLeft(9).PaddingRight(10).Column(card =>
            {
                card.Item().Row(header);
                card.Item().PaddingTop(3).Text(message);
            });
        });
    }

    private static void NoMistakes(IContainer container)
    {
        container.Border(0.75f).BorderColor("#bfe6cf").Background(SuccessTint).CornerRadius(8).PaddingVertical(8).PaddingHorizontal(12).Row(row =>
        {
            row.AutoItem().AlignMiddle().Width(12).Height(12).Svg(CircleCheck);
            row.RelativeItem().PaddingLeft(6).AlignMiddle().Text("No mistakes found.").FontSize(9).FontColor(SuccessInk);
        });
    }

    private static void PointsPill(IContainer container, double points)
    {
        container.AlignLeft().Background(DangerTint).CornerRadius(3).PaddingHorizontal(4).PaddingVertical(0.5f)
            .Text($"−{Number(points)}").FontSize(8.5f).Bold().FontFamily(Mono).FontColor(DangerInk);
    }

    private static void Section(ColumnDescriptor column, string title, IReadOnlyList<Action<IContainer>> items)
    {
        column.Item().ShowEntire().Column(first =>
        {
            first.Item().PaddingTop(12).Element(c => Eyebrow(c, title));
            first.Item().Element(items[0]);
        });

        foreach (var item in items.Skip(1))
        {
            column.Item().ShowEntire().Element(item);
        }
    }

    private static void Eyebrow(IContainer container, string title)
    {
        container.PaddingTop(6).PaddingBottom(7).Text(title.ToUpperInvariant()).FontSize(7.5f).SemiBold().LetterSpacing(0.08f).FontColor(Muted);
    }

    private static void StatusChip(IContainer container, GradingDocumentStatus status)
    {
        var (label, background, ink, border) = status switch
        {
            GradingDocumentStatus.Approved => ("Approved", SuccessTint, SuccessInk, SuccessTint),
            GradingDocumentStatus.InReview => ("In review", AccentTint, AccentStrong, AccentTint),
            GradingDocumentStatus.Discarded => ("Discarded", "#ffffff", Muted, Faint),
            _ => ("AI draft, not reviewed", PaperDark, Muted, PaperDark),
        };

        container.AlignLeft().Border(0.75f).BorderColor(border).Background(background).CornerRadius(4)
            .PaddingVertical(2).PaddingHorizontal(6).Text(label).FontSize(8).SemiBold().FontColor(ink).LineHeight(1.2f);
    }

    private static void Avatar(IContainer container, string name)
    {
        var initials = string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.IsAsciiLetterLower(w[0]) ? char.ToUpperInvariant(w[0]) : w[0]));
        container.Width(20).Height(20).Background(AccentTint).CornerRadius(10).AlignCenter().AlignMiddle()
            .Text(initials).FontSize(7.5f).Bold().FontColor(AccentStrong).LineHeight(1);
    }

    private static void Markdown(IContainer container, string markdown)
    {
        var parsed = Markdig.Markdown.Parse(markdown, Pipeline);
        container.Column(column => Blocks(column, parsed));
    }

    private static void Blocks(ColumnDescriptor column, ContainerBlock blocks)
    {
        column.Spacing(6);
        foreach (var block in blocks)
        {
            column.Item().Element(c => MarkdownBlock(c, block));
        }
    }

    private static void MarkdownBlock(IContainer container, Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var size = heading.Level switch { 1 => 12.5f, 2 => 11f, _ => 10f };
                container.PaddingTop(4).Text(text =>
                {
                    text.DefaultTextStyle(s => s.FontSize(size).SemiBold().FontColor(InkSoft));
                    Inlines(text, heading.Inline, false, false);
                });
                break;
            case ParagraphBlock paragraph:
                container.Text(text => Inlines(text, paragraph.Inline, false, false));
                break;
            case Table table:
                MarkdownTable(container, table);
                break;
            case ListBlock list:
                MarkdownList(container, list);
                break;
            case QuoteBlock quote:
                container.BorderLeft(2).BorderColor(Line).PaddingLeft(8).Column(inner => Blocks(inner, quote));
                break;
            case ThematicBreakBlock:
                container.PaddingVertical(2).LineHorizontal(0.75f).LineColor(Line);
                break;
            case CodeBlock code:
                container.Background(Paper).Border(0.75f).BorderColor(Line).CornerRadius(3).Padding(8)
                    .Text(code.Lines.ToString().TrimEnd()).FontFamily(Mono).FontSize(8).FontColor(InkSoft);
                break;
            case ContainerBlock children:
                container.Column(inner => Blocks(inner, children));
                break;
            case LeafBlock { Inline: { } inline }:
                container.Text(text => Inlines(text, inline, false, false));
                break;
        }
    }

    private static void MarkdownList(IContainer container, ListBlock list)
    {
        var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
        container.Column(column =>
        {
            column.Spacing(3);
            foreach (var item in list.OfType<ListItemBlock>())
            {
                var marker = list.IsOrdered ? $"{number++}." : "•";
                column.Item().Row(row =>
                {
                    row.ConstantItem(14).Text(marker).FontColor(Muted);
                    row.RelativeItem().Column(inner => Blocks(inner, item));
                });
            }
        });
    }

    private static void MarkdownTable(IContainer container, Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        var columns = Math.Max(1, rows.Count == 0 ? 1 : rows.Max(r => r.Count));
        var widths = Enumerable.Range(0, columns)
            .Select(i => rows.Select(r => i < r.Count ? PlainLength(r[i]) : 0).DefaultIfEmpty(0).Max())
            .Select(length => length <= 16 ? length + 8f : Math.Min(length, 50))
            .ToList();

        container.Border(0.75f).BorderColor(Line).CornerRadius(4).Table(grid =>
        {
            grid.ColumnsDefinition(definition =>
            {
                foreach (var width in widths)
                {
                    definition.RelativeColumn(width);
                }
            });

            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                var lastRow = r == rows.Count - 1;
                for (var i = 0; i < columns; i++)
                {
                    var cell = grid.Cell().Row((uint)r + 1).Column((uint)i + 1)
                        .Element(c => row.IsHeader ? c.Background(Paper) : c)
                        .Element(c => lastRow ? c : c.BorderBottom(0.75f).BorderColor(Line))
                        .PaddingVertical(4).PaddingHorizontal(6)
                        .DefaultTextStyle(s => row.IsHeader ? s.FontSize(7.5f).SemiBold().FontColor(Muted) : s.FontSize(8.5f));
                    if (i < row.Count && row[i] is TableCell tableCell)
                    {
                        cell.Column(inner => Blocks(inner, tableCell));
                    }
                    else
                    {
                        cell.Text("");
                    }
                }
            }
        });
    }

    private static void Inlines(TextDescriptor text, ContainerInline? inlines, bool bold, bool italic)
    {
        if (inlines is null)
        {
            return;
        }

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    Styled(text.Span(literal.Content.ToString()), bold, italic);
                    break;
                case EmphasisInline emphasis:
                    Inlines(text, emphasis, bold || emphasis.DelimiterCount >= 2, italic || emphasis.DelimiterCount == 1);
                    break;
                case CodeInline code:
                    text.Span(code.Content).FontFamily(Mono).FontSize(8.5f).BackgroundColor(Paper).FontColor(InkSoft);
                    break;
                case LineBreakInline lineBreak:
                    text.Span(lineBreak.IsHard ? "\n" : " ");
                    break;
                case ContainerInline container:
                    Inlines(text, container, bold, italic);
                    break;
                case HtmlEntityInline entity:
                    Styled(text.Span(entity.Transcoded.ToString()), bold, italic);
                    break;
            }
        }
    }

    private static void Styled(TextSpanDescriptor span, bool bold, bool italic)
    {
        if (bold)
        {
            span.SemiBold();
        }

        if (italic)
        {
            span.Italic();
        }
    }

    private static int PlainLength(Block block)
    {
        return block.Descendants<LiteralInline>().Sum(l => l.Content.Length) + block.Descendants<CodeInline>().Sum(c => c.Content.Length);
    }

    private static string? Location(string? file, int? startLine, int? endLine)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }

        if (startLine is not { } start)
        {
            return file;
        }

        return endLine is { } end && end != start ? $"{file}:{start}–{end}" : $"{file}:{start}";
    }

    private static string Category(MistakeCategory category)
    {
        var words = CamelWords().Replace(category.ToString(), " $1").Trim();
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..].ToLowerInvariant();
    }

    private static string Number(double value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string Date(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex CamelWords();
}
