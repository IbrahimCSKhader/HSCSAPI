using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HSCSAPI.Services.Laboratory;

public class LabResultPdfGenerator : ILabResultPdfGenerator
{
    private static readonly string Teal = Colors.Teal.Darken2;
    private static readonly string Border = Colors.Grey.Lighten2;
    private static readonly string Muted = Colors.Grey.Darken1;

    public byte[] Generate(LabResultPdfDocument document)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken4));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(column =>
                        {
                            column.Item().Text("SHCS LABORATORY REPORT").FontSize(18).Bold().FontColor(Teal);
                            column.Item().Text(document.TestName).FontSize(12).SemiBold();
                        });
                        row.ConstantItem(120).AlignRight().Column(column =>
                        {
                            column.Item().Text("FINAL RESULT").Bold().FontColor(Teal);
                            column.Item().Text($"Accession: {document.AccessionNumber}").FontSize(8);
                        });
                    });
                    header.Item().PaddingTop(10).LineHorizontal(1).LineColor(Teal);
                });

                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Element(content => PatientAndSpecimen(content, document));
                    column.Item().Text("RESULTS").Bold().FontColor(Teal);
                    column.Item().Element(content => ResultsTable(content, document.Values));

                    if (!string.IsNullOrWhiteSpace(document.Comments))
                    {
                        column.Item().Column(notes =>
                        {
                            notes.Item().Text("COMMENTS").Bold().FontColor(Teal);
                            notes.Item().Border(1).BorderColor(Border).Padding(8).Text(document.Comments);
                        });
                    }

                    column.Item().PaddingTop(2).Text(
                            "Reference ranges may vary by laboratory method, age, sex, and clinical context. Results require clinical interpretation.")
                        .FontSize(8)
                        .FontColor(Muted);
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"Reported {document.CompletedAt:yyyy-MM-dd HH:mm} UTC").FontSize(8).FontColor(Muted);
                    row.RelativeItem().AlignRight().Text(text =>
                    {
                        text.Span("Page ").FontSize(8).FontColor(Muted);
                        text.CurrentPageNumber().FontSize(8).FontColor(Muted);
                        text.Span(" of ").FontSize(8).FontColor(Muted);
                        text.TotalPages().FontSize(8).FontColor(Muted);
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void PatientAndSpecimen(IContainer container, LabResultPdfDocument document)
    {
        container.Border(1).BorderColor(Border).Padding(10).Column(column =>
        {
            column.Spacing(5);
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(x => Labeled(x, "Patient", document.PatientName));
                row.RelativeItem().Element(x => Labeled(x, "Patient ID", document.PatientUserId));
                row.RelativeItem().Element(x => Labeled(x, "Date of birth", document.DateOfBirth ?? "Not recorded"));
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(x => Labeled(x, "Gender", document.Gender ?? "Not recorded"));
                row.RelativeItem().Element(x => Labeled(x, "Specimen", document.SpecimenType));
                row.RelativeItem().Element(x => Labeled(x, "Condition", document.SpecimenCondition));
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(x => Labeled(x, "Collected", $"{document.CollectedAt:yyyy-MM-dd HH:mm} UTC"));
                row.RelativeItem().Element(x => Labeled(x, "Received", $"{document.ReceivedAt:yyyy-MM-dd HH:mm} UTC"));
                row.RelativeItem().Element(x => Labeled(x, "LOINC", document.LoincCode ?? "Local panel"));
            });
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(x => Labeled(x, "Ordering doctor", document.DoctorName));
                row.RelativeItem().Element(x => Labeled(x, "Technologist", document.LaboratoryTechnologistName));
                row.RelativeItem().Element(x => Labeled(x, "Laboratory", document.ClinicName));
            });

            if (!string.IsNullOrWhiteSpace(document.SpecimenNotes))
            {
                column.Item().Element(x => Labeled(x, "Specimen notes", document.SpecimenNotes));
            }
        });
    }

    private static void ResultsTable(IContainer container, IReadOnlyList<LabResultPdfValue> values)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(1.2f);
                columns.RelativeColumn(0.8f);
                columns.RelativeColumn(1f);
                columns.RelativeColumn(1.5f);
            });

            table.Header(header =>
            {
                HeaderCell(header, "Test");
                HeaderCell(header, "Result");
                HeaderCell(header, "Flag");
                HeaderCell(header, "Unit");
                HeaderCell(header, "Reference range");
            });

            foreach (var value in values)
            {
                BodyCell(table, value.Label);
                BodyCell(table, value.Value, bold: true);
                BodyCell(table, value.Flag ?? string.Empty, flag: value.Flag);
                BodyCell(table, value.Unit ?? string.Empty);
                BodyCell(table, value.ReferenceRange ?? "Laboratory-specific");
            }
        });
    }

    private static void HeaderCell(TableCellDescriptor header, string text) =>
        header.Cell().Background(Teal).Padding(6).Text(text).Bold().FontColor(Colors.White).FontSize(8);

    private static void BodyCell(TableDescriptor table, string text, bool bold = false, string? flag = null)
    {
        var cell = table.Cell().BorderBottom(1).BorderColor(Border).Padding(6);
        var textBlock = cell.Text(text).FontSize(8);
        if (bold)
        {
            textBlock.Bold();
        }

        if (!string.IsNullOrWhiteSpace(flag) && !flag.Equals("Normal", StringComparison.OrdinalIgnoreCase))
        {
            textBlock.Bold().FontColor(Colors.Red.Darken1);
        }
    }

    private static void Labeled(IContainer container, string label, string value)
    {
        container.Column(column =>
        {
            column.Item().Text(label.ToUpperInvariant()).FontSize(7).FontColor(Muted);
            column.Item().Text(value).SemiBold();
        });
    }
}
