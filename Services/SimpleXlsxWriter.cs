using System.IO.Compression;
using System.Text;

namespace DentalClinic.Services;

/// <summary>
/// Пишет настоящий .xlsx (Open XML) одним листом, используя только System.IO.Compression —
/// без ClosedXML/EPPlus. Специально сделано без внешних NuGet-пакетов, чтобы фича
/// заработала сразу после копирования файла, без "dotnet restore" новых зависимостей.
/// Годится для простых табличных отчётов (не для формул/стилей/графиков).
/// </summary>
public static class SimpleXlsxWriter
{
    public static byte[] Write(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var mem = new MemoryStream();
        using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "[Content_Types].xml", ContentTypesXml());
            WriteEntry(zip, "_rels/.rels", RelsXml());
            WriteEntry(zip, "xl/workbook.xml", WorkbookXml(sheetName));
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml());
            WriteEntry(zip, "xl/worksheets/sheet1.xml", SheetXml(headers, rows));
        }
        return mem.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Esc(string? s) =>
        System.Security.SecurityElement.Escape(s ?? "") ?? "";

    private static string ContentTypesXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private static string RelsXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string WorkbookRelsXml() => """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;

    private static string WorkbookXml(string sheetName) => $"""
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="{Esc(sheetName)}" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private static string SheetXml(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");

        AppendRow(sb, 1, headers);
        for (int i = 0; i < rows.Count; i++)
            AppendRow(sb, i + 2, rows[i]);

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowIndex, IReadOnlyList<string> cells)
    {
        sb.Append($"<row r=\"{rowIndex}\">");
        for (int c = 0; c < cells.Count; c++)
        {
            var cellRef = $"{ColumnLetter(c + 1)}{rowIndex}";
            sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(cells[c])}</t></is></c>");
        }
        sb.Append("</row>");
    }

    private static string ColumnLetter(int col)
    {
        var s = "";
        while (col > 0)
        {
            var rem = (col - 1) % 26;
            s = (char)('A' + rem) + s;
            col = (col - 1) / 26;
        }
        return s;
    }
}