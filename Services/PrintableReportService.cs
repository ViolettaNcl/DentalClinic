using System.Net;
using System.Text;

namespace DentalClinic.Services;

/// <summary>
/// Генерирует печатную HTML-версию отчёта: администратор открывает её в новой вкладке
/// и жмёт Ctrl+P → "Сохранить как PDF". Через systemный диалог печати браузера кириллица
/// рендерится нормально (в отличие от самодельного PDF без встроенных шрифтов).
/// Если в будущем понадобится PDF именно с сервера (например, для автоматической
/// рассылки по email) — тогда стоит подключить нормальную библиотеку (QuestPDF/PDFsharp)
/// через NuGet, здесь это осознанно не сделано.
/// </summary>
public static class PrintableReportService
{
    public static string BuildReportHtml(string title, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.Append($$"""
        <!DOCTYPE html>
        <html lang="ru">
        <head>
        <meta charset="UTF-8">
        <title>{{WebUtility.HtmlEncode(title)}}</title>
        <style>
            body {font-family: 'Segoe UI', Arial, sans-serif; color: #1a1a1a; margin: 32px; }
            h1 {font-size: 20px; margin-bottom: 4px; }
            .meta {color: #666; font-size: 13px; margin-bottom: 20px; }
            table {width: 100%; border-collapse: collapse; font-size: 13px; }
            th, td {border: 1px solid #ddd; padding: 6px 10px; text-align: left; }
            th {background: #13b39b; color: #fff; }
            tr:nth-child(even) {background: #f7f7f7; }
            .print-btn {margin-bottom: 20px; padding: 8px 16px; background:#13b39b; color:#fff; border:none; border-radius:6px; cursor:pointer; font-size:14px; }
            @media print { .print-btn { display:none; } }
        </style>
        </head>
        <body>
        <button class="print-btn" onclick="window.print()">🖨️ Сохранить как PDF</button>
        <h1>{{WebUtility.HtmlEncode(title)}}</h1>
        <div class="meta">Сформировано: {{DateTime.Now:dd.MM.yyyy HH:mm}}</div>
        <table><thead><tr>
        """);

        foreach (var h in headers)
            sb.Append($"<th>{WebUtility.HtmlEncode(h)}</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            sb.Append("<tr>");
            foreach (var cell in row)
                sb.Append($"<td>{WebUtility.HtmlEncode(cell)}</td>");
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table></body></html>");
        return sb.ToString();
    }
}