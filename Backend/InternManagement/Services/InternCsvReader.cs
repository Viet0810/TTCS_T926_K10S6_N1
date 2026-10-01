using System.Text;

namespace InternManagement.Services;

/// <summary>Reads a UTF-8 CSV file using RFC 4180 quoting rules.</summary>
public static class InternCsvReader
{
    public static IReadOnlyList<IReadOnlyDictionary<int, string>> Read(Stream stream)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = reader.ReadToEnd();
        var rows = new List<IReadOnlyDictionary<int, string>>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        void FinishField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void FinishRow()
        {
            FinishField();
            rows.Add(fields.Select((value, index) => (value, index)).ToDictionary(item => item.index, item => item.value));
            fields.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"') quoted = false;
                else field.Append(c);
                continue;
            }

            if (c == '"' && field.Length == 0) quoted = true;
            else if (c == ',') FinishField();
            else if (c == '\r' || c == '\n')
            {
                FinishRow();
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
            else field.Append(c);
        }

        if (quoted) throw new InvalidDataException("CSV có dấu ngoặc kép chưa được đóng.");
        if (field.Length > 0 || fields.Count > 0) FinishRow();
        return rows;
    }
}
