/**
 * attendance-report-service.js
 * Quản lý dữ liệu và logic Báo cáo Chuyên cần & Nghỉ phép (K10S6N1-92 / K10S6N1-60)
 */

const AttendanceService = {
  async getInterns() {
    const live = await API.getInterns();
    return live.map(item => ({
      id: item.id, mssv: item.studentCode || "", name: item.fullName,
      email: item.email, department: item.department || item.major || "", school: item.school,
    }));
  },

  async getAttendanceReport(filters = {}) {
    const params = new URLSearchParams();
    if (filters.internId) params.set("internId", filters.internId);
    if (filters.keyword) params.set("search", filters.keyword);
    if (filters.startDate) params.set("startDate", filters.startDate);
    if (filters.endDate) params.set("endDate", filters.endDate);
    if (filters.status && filters.status !== "ALL") params.set("status", filters.status);
    return requestApi(`/attendance/report?${params}`, { headers: getAuthHeader() });
  },

  getStatusLabel(status) {
    const map = {
      ON_TIME: { label: "Đúng giờ", className: "status-ontime" },
      LATE: { label: "Đi muộn", className: "status-late" },
      EARLY: { label: "Về sớm", className: "status-early" },
      LEAVE_APPROVED: { label: "Nghỉ có phép", className: "status-leave-approved" },
      ABSENT: { label: "Nghỉ không phép", className: "status-absent" },
    };
    return map[status] || { label: status, className: "status-ontime" };
  },

  exportCSV(records) {
    const headers = [
      "STT",
      "Ngày",
      "Mã sinh viên",
      "Họ và tên",
      "Đơn vị / Bộ phận",
      "Giờ vào",
      "Giờ ra",
      "Số giờ làm",
      "Trạng thái chuyên cần",
      "Ghi chú / Lý do",
      "Người duyệt",
    ];

    const rows = records.map((r, idx) => [
      idx + 1,
      r.date,
      `"${r.mssv}"`,
      `"${r.name}"`,
      `"${r.department}"`,
      r.checkIn,
      r.checkOut,
      r.hours,
      `"${this.getStatusLabel(r.status).label}"`,
      `"${(r.note || "").replace(/"/g, '""')}"`,
      `"${(r.approver || "").replace(/"/g, '""')}"`,
    ]);

    const csvContent = "\uFEFF" + [headers.join(","), ...rows.map((row) => row.join(","))].join("\r\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const fileName = `Bao_cao_chuyen_can_${new Date().toISOString().slice(0, 10)}.csv`;

    if (typeof saveDownload === "function") {
      saveDownload(blob, fileName);
    } else {
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
  },
};
