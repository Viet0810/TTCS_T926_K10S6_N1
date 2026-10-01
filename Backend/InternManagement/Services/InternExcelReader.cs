using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace InternManagement.Services;

/// <summary>Reads the first worksheet from a standard Office Open XML .xlsx workbook.</summary>
public static class InternExcelReader
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static IReadOnlyList<IReadOnlyDictionary<int, string>> Read(Stream input)
    {
        using var archive = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var firstSheet = workbook.Root?.Element(Main + "sheets")?.Elements(Main + "sheet").FirstOrDefault()
            ?? throw new InvalidDataException("Workbook không có worksheet.");
        var relationId = (string?)firstSheet.Attribute(OfficeRel + "id")
            ?? throw new InvalidDataException("Không xác định được worksheet đầu tiên.");
        var rels = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        var target = (string?)rels.Root?.Elements(PackageRel + "Relationship")
            .FirstOrDefault(x => (string?)x.Attribute("Id") == relationId)?.Attribute("Target")
            ?? throw new InvalidDataException("Không tìm thấy dữ liệu worksheet.");
        var sheetPath = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.TrimStart('/');

        var sharedStrings = new List<string>();
        var sharedEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedEntry is not null)
        {
            using var stream = sharedEntry.Open();
            var strings = XDocument.Load(stream);
            sharedStrings.AddRange(strings.Root?.Elements(Main + "si")
                .Select(si => string.Concat(si.Descendants(Main + "t").Select(t => t.Value))) ?? []);
        }

        var sheet = ReadXml(archive, sheetPath);
        var result = new List<IReadOnlyDictionary<int, string>>();
        foreach (var row in sheet.Descendants(Main + "sheetData").Elements(Main + "row"))
        {
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements(Main + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                if (string.IsNullOrEmpty(reference)) continue;
                var column = ColumnIndex(reference);
                var type = (string?)cell.Attribute("t");
                var value = cell.Element(Main + "v")?.Value ?? "";
                if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                    value = sharedStrings[sharedIndex];
                else if (type == "inlineStr")
                    value = string.Concat(cell.Descendants(Main + "t").Select(t => t.Value));
                else if (type == "b")
                    value = value == "1" ? "TRUE" : "FALSE";
                cells[column] = value.Trim();
            }
            result.Add(cells);
        }
        return result;
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path.Replace('\\', '/'))
            ?? throw new InvalidDataException($"Tệp Excel thiếu thành phần {path}.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static int ColumnIndex(string cellReference)
    {
        var value = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character)) break;
            value = value * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }
        return value - 1;
    }
}
