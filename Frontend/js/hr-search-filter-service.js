const InternService = {
  getInterns(filters = {}) {
    const query = new URLSearchParams();
    for (const key of ["search", "school", "major"]) if (filters[key]) query.set(key, filters[key]);
    return requestApi(`/interns/search?${query}`, { headers: getAuthHeader() });
  },
  exportCSV(interns) {
    // Quoting and neutralizing formula prefixes prevents spreadsheet injection.
    const quote = (value) => {
      let text = String(value ?? "");
      if (/^[\s]*[=+@-]/.test(text)) text = "'" + text;
      return '"' + text.replace(/"/g, '""') + '"';
    };
    const fields = InternProfile.fields.map((field) => field.key);
    const rows = [InternProfile.fields.map((field) => field.label), ...interns.map((intern) => fields.map((key) => intern[key]))];
    const blob = new Blob(["\uFEFF" + rows.map((row) => row.map(quote).join(",")).join("\r\n")], { type: "text/csv;charset=utf-8" });
    saveDownload(blob, "Danh_sach_thuc_tap_sinh.csv");
  }
};
