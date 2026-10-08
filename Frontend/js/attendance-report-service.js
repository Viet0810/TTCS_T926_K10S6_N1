/**
 * attendance-report-service.js
 * Quản lý dữ liệu và logic Báo cáo Chuyên cần & Nghỉ phép (K10S6N1-92 / K10S6N1-60)
 */

const ATTENDANCE_STORAGE_KEY = "ims_attendance_records_v1";

const DEFAULT_INTERNS = [
  { id: 1, mssv: "SV2024001", name: "Nguyễn Hoàng Nam", email: "nam.nh210678@sis.hust.edu.vn", department: "Phát triển phần mềm .NET", school: "ĐH Bách Khoa Hà Nội" },
  { id: 2, mssv: "SV2024002", name: "Trần Mai Phương", email: "phuong.tm@vnu.edu.vn", department: "Kiểm thử phần mềm (QA/QC)", school: "ĐH Công Nghệ - ĐHQGHN" },
  { id: 3, mssv: "SV2024003", name: "Lê Quốc Bảo", email: "baolq.ptit@gmail.com", department: "An ninh mạng & Hệ thống", school: "Học Viện Công Nghệ Bưu Chính Viễn Thông" },
  { id: 4, mssv: "SV2024004", name: "Phạm Thùy Linh", email: "linhpt.fpt@fe.edu.vn", department: "Phát triển Web Frontend", school: "Đại Học FPT" },
  { id: 5, mssv: "SV2024005", name: "Vũ Hải Đăng", email: "dang.vh215542@sis.hust.edu.vn", department: "Kỹ thuật dữ liệu & AI", school: "ĐH Bách Khoa Hà Nội" },
  { id: 6, mssv: "SV2024006", name: "Đặng Thị Ngọc Ánh", email: "anh.dtn@neu.edu.vn", department: "Phân tích nghiệp vụ (BA)", school: "ĐH Kinh Tế Quốc Dân" },
  { id: 7, mssv: "SV2024007", name: "Bùi Tuấn Anh", email: "anh.bui@hcmut.edu.vn", department: "DevOps & Cloud", school: "ĐH Bách Khoa TP.HCM" },
  { id: 8, mssv: "SV2024008", name: "Hoàng Minh Trí", email: "tri.hm@vnu.edu.vn", department: "Phát triển Backend Java", school: "ĐH Công Nghệ - ĐHQGHN" },
  { id: 9, mssv: "SV2024009", name: "Ngô Mỹ Duyên", email: "duyennm@ptit.edu.vn", department: "Thiết kế UI/UX", school: "Học Viện Công Nghệ Bưu Chính Viễn Thông" },
  { id: 10, mssv: "SV2024010", name: "Trịnh Quang Huy", email: "huytq@fpt.edu.vn", department: "Phát triển Web Fullstack", school: "Đại Học FPT" },
  { id: 11, mssv: "SV2024011", name: "Cao Thảo Vân", email: "van.ct218901@sis.hust.edu.vn", department: "Phát triển phần mềm .NET", school: "ĐH Bách Khoa Hà Nội" },
  { id: 12, mssv: "SV2024012", name: "Lâm Gia Kiệt", email: "kietlg@vnu.edu.vn", department: "An ninh mạng & Hệ thống", school: "ĐH Công Nghệ - ĐHQGHN" },
];

const INITIAL_ATTENDANCE_RECORDS = [
  // Ngày 05/10/2026 (Hôm nay)
  { id: 101, internId: 1, mssv: "SV2024001", name: "Nguyễn Hoàng Nam", department: "Phát triển phần mềm .NET", date: "2026-10-05", checkIn: "08:24", checkOut: "17:31", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 102, internId: 2, mssv: "SV2024002", name: "Trần Mai Phương", department: "Kiểm thử phần mềm (QA/QC)", date: "2026-10-05", checkIn: "08:52", checkOut: "17:35", hours: 7.7, status: "LATE", note: "Kẹt xe tuyến đường Cầu Giấy", approver: "Nguyễn Vũ Long (Mentor)" },
  { id: 103, internId: 3, mssv: "SV2024003", name: "Lê Quốc Bảo", department: "An ninh mạng & Hệ thống", date: "2026-10-05", checkIn: "08:20", checkOut: "16:15", hours: 6.9, status: "EARLY", note: "Xin phép về sớm 1h làm thủ tục đồ án", approver: "Trần Văn Minh (Mentor)" },
  { id: 104, internId: 4, mssv: "SV2024004", name: "Phạm Thùy Linh", department: "Phát triển Web Frontend", date: "2026-10-05", checkIn: "—", checkOut: "—", hours: 0.0, status: "LEAVE_APPROVED", note: "Nghỉ ốm (Đã gửi giấy chứng nhận y tế)", approver: "Hoàng Lan Anh (HR)" },
  { id: 105, internId: 5, mssv: "SV2024005", name: "Vũ Hải Đăng", department: "Kỹ thuật dữ liệu & AI", date: "2026-10-05", checkIn: "08:28", checkOut: "17:30", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 106, internId: 6, mssv: "SV2024006", name: "Đặng Thị Ngọc Ánh", department: "Phân tích nghiệp vụ (BA)", date: "2026-10-05", checkIn: "08:25", checkOut: "17:40", hours: 8.2, status: "ON_TIME", note: "Tham gia họp Sprint Review với khách hàng" },
  { id: 107, internId: 10, mssv: "SV2024010", name: "Trịnh Quang Huy", department: "Phát triển Web Fullstack", date: "2026-10-05", checkIn: "08:22", checkOut: "17:30", hours: 8.1, status: "ON_TIME", note: "Hoàn thiện module giao diện báo cáo" },
  { id: 108, internId: 11, mssv: "SV2024011", name: "Cao Thảo Vân", department: "Phát triển phần mềm .NET", date: "2026-10-05", checkIn: "08:30", checkOut: "17:30", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },

  // Ngày 04/10/2026 (Chủ nhật - OT dự án)
  { id: 109, internId: 1, mssv: "SV2024001", name: "Nguyễn Hoàng Nam", department: "Phát triển phần mềm .NET", date: "2026-10-04", checkIn: "09:00", checkOut: "13:00", hours: 4.0, status: "ON_TIME", note: "Tăng ca triển khai bản vá lỗi khẩn cấp" },

  // Ngày 02/10/2026 (Thứ sáu)
  { id: 110, internId: 1, mssv: "SV2024001", name: "Nguyễn Hoàng Nam", department: "Phát triển phần mềm .NET", date: "2026-10-02", checkIn: "08:25", checkOut: "17:30", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 111, internId: 2, mssv: "SV2024002", name: "Trần Mai Phương", department: "Kiểm thử phần mềm (QA/QC)", date: "2026-10-02", checkIn: "08:20", checkOut: "17:35", hours: 8.2, status: "ON_TIME", note: "Kiểm thử hồi quy Sprint 1" },
  { id: 112, internId: 3, mssv: "SV2024003", name: "Lê Quốc Bảo", department: "An ninh mạng & Hệ thống", date: "2026-10-02", checkIn: "—", checkOut: "—", hours: 0.0, status: "ABSENT", note: "Không có mặt, chưa nộp đơn xin phép", approver: "Chưa duyệt" },
  { id: 113, internId: 4, mssv: "SV2024004", name: "Phạm Thùy Linh", department: "Phát triển Web Frontend", date: "2026-10-02", checkIn: "08:29", checkOut: "17:30", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 114, internId: 7, mssv: "SV2024007", name: "Bùi Tuấn Anh", department: "DevOps & Cloud", date: "2026-10-02", checkIn: "08:20", checkOut: "17:45", hours: 8.4, status: "ON_TIME", note: "Bảo trì hạ tầng máy chủ nội bộ" },
  { id: 115, internId: 8, mssv: "SV2024008", name: "Hoàng Minh Trí", department: "Phát triển Backend Java", date: "2026-10-02", checkIn: "08:48", checkOut: "17:30", hours: 7.7, status: "LATE", note: "Sự cố mạng internet tại nhà khi họp sáng", approver: "Trần Văn Minh (Mentor)" },
  { id: 116, internId: 9, mssv: "SV2024009", name: "Ngô Mỹ Duyên", department: "Thiết kế UI/UX", date: "2026-10-02", checkIn: "08:18", checkOut: "17:30", hours: 8.2, status: "ON_TIME", note: "Thiết kế wireframe tính năng mới" },
  { id: 117, internId: 10, mssv: "SV2024010", name: "Trịnh Quang Huy", department: "Phát triển Web Fullstack", date: "2026-10-02", checkIn: "08:26", checkOut: "17:32", hours: 8.1, status: "ON_TIME", note: "Tham gia buổi demo nội bộ nhóm" },

  // Ngày 01/10/2026 (Thứ năm)
  { id: 118, internId: 1, mssv: "SV2024001", name: "Nguyễn Hoàng Nam", department: "Phát triển phần mềm .NET", date: "2026-10-01", checkIn: "08:25", checkOut: "17:30", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 119, internId: 2, mssv: "SV2024002", name: "Trần Mai Phương", department: "Kiểm thử phần mềm (QA/QC)", date: "2026-10-01", checkIn: "08:27", checkOut: "17:30", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 120, internId: 5, mssv: "SV2024005", name: "Vũ Hải Đăng", department: "Kỹ thuật dữ liệu & AI", date: "2026-10-01", checkIn: "—", checkOut: "—", hours: 0.0, status: "LEAVE_APPROVED", note: "Nghỉ thi tốt nghiệp chứng chỉ tiếng Anh", approver: "Nguyễn Vũ Long (Mentor)" },
  { id: 121, internId: 6, mssv: "SV2024006", name: "Đặng Thị Ngọc Ánh", department: "Phân tích nghiệp vụ (BA)", date: "2026-10-01", checkIn: "08:15", checkOut: "17:30", hours: 8.2, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 122, internId: 11, mssv: "SV2024011", name: "Cao Thảo Vân", department: "Phát triển phần mềm .NET", date: "2026-10-01", checkIn: "08:50", checkOut: "17:40", hours: 7.8, status: "LATE", note: "Đi mưa ngập đường Kim Mã", approver: "Hoàng Lan Anh (HR)" },
  { id: 123, internId: 12, mssv: "SV2024012", name: "Lâm Gia Kiệt", department: "An ninh mạng & Hệ thống", date: "2026-10-01", checkIn: "08:22", checkOut: "17:30", hours: 8.1, status: "ON_TIME", note: "Làm việc tại văn phòng" },

  // Ngày 30/09/2026 (Cuối tháng 9)
  { id: 124, internId: 1, mssv: "SV2024001", name: "Nguyễn Hoàng Nam", department: "Phát triển phần mềm .NET", date: "2026-09-30", checkIn: "08:25", checkOut: "17:30", hours: 8.0, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 125, internId: 4, mssv: "SV2024004", name: "Phạm Thùy Linh", department: "Phát triển Web Frontend", date: "2026-09-30", checkIn: "08:21", checkOut: "17:35", hours: 8.2, status: "ON_TIME", note: "Làm việc tại văn phòng" },
  { id: 126, internId: 7, mssv: "SV2024007", name: "Bùi Tuấn Anh", department: "DevOps & Cloud", date: "2026-09-30", checkIn: "—", checkOut: "—", hours: 0.0, status: "LEAVE_APPROVED", note: "Về quê xử lý việc gia đình", approver: "Trần Văn Minh (Mentor)" },
  { id: 127, internId: 10, mssv: "SV2024010", name: "Trịnh Quang Huy", department: "Phát triển Web Fullstack", date: "2026-09-30", checkIn: "08:19", checkOut: "17:30", hours: 8.1, status: "ON_TIME", note: "Làm việc tại văn phòng" },
];

const AttendanceService = {
  USE_API: false, // Bật true khi Backend triển khai xong subtask K10S6N1-91
  API_BASE_URL: "http://localhost:5024/api",

  initStorage() {
    if (!localStorage.getItem(ATTENDANCE_STORAGE_KEY)) {
      localStorage.setItem(ATTENDANCE_STORAGE_KEY, JSON.stringify(INITIAL_ATTENDANCE_RECORDS));
    }
  },

  getAllRecords() {
    this.initStorage();
    try {
      const data = localStorage.getItem(ATTENDANCE_STORAGE_KEY);
      return data ? JSON.parse(data) : [...INITIAL_ATTENDANCE_RECORDS];
    } catch {
      return [...INITIAL_ATTENDANCE_RECORDS];
    }
  },

  async getInterns() {
    try {
      if (typeof API !== "undefined" && typeof API.getInterns === "function") {
        const live = await API.getInterns();
        if (Array.isArray(live) && live.length > 0) {
          return live.map((item, idx) => ({
            id: item.id || idx + 1,
            mssv: item.studentCode || item.mssv || `SV2024${String(item.id || idx + 1).padStart(3, "0")}`,
            name: item.fullName || item.name || "Thực tập sinh",
            email: item.email || "",
            department: item.organization || item.major || "Thực tập",
            school: item.school || "",
          }));
        }
      }
    } catch (e) {
      console.info("[attendance-service] Dùng danh sách sinh viên nội bộ dự phòng:", e.message);
    }
    return DEFAULT_INTERNS;
  },

  async getAttendanceReport(filters = {}) {
    let list = this.getAllRecords();

    // 1. Lọc theo thực tập sinh cụ thể
    if (filters.internId) {
      const targetId = Number(filters.internId);
      list = list.filter((r) => r.internId === targetId);
    }

    // 2. Lọc theo từ khóa (Tên, MSSV, Bộ phận)
    if (filters.keyword) {
      const kw = filters.keyword.trim().toLowerCase();
      list = list.filter((r) =>
        r.name.toLowerCase().includes(kw) ||
        r.mssv.toLowerCase().includes(kw) ||
        r.department.toLowerCase().includes(kw)
      );
    }

    // 3. Lọc theo ngày bắt đầu (startDate: YYYY-MM-DD)
    if (filters.startDate) {
      list = list.filter((r) => r.date >= filters.startDate);
    }

    // 4. Lọc theo ngày kết thúc (endDate: YYYY-MM-DD)
    if (filters.endDate) {
      list = list.filter((r) => r.date <= filters.endDate);
    }

    // 5. Lọc theo trạng thái chuyên cần
    if (filters.status && filters.status !== "ALL") {
      list = list.filter((r) => r.status === filters.status);
    }

    // Sắp xếp ngày giảm dần (mới nhất lên đầu)
    list.sort((a, b) => b.date.localeCompare(a.date));

    // Tính toán chỉ số KPI
    const totalShifts = list.length;
    const onTimeCount = list.filter((r) => r.status === "ON_TIME").length;
    const lateCount = list.filter((r) => r.status === "LATE").length;
    const earlyCount = list.filter((r) => r.status === "EARLY").length;
    const approvedLeaveCount = list.filter((r) => r.status === "LEAVE_APPROVED").length;
    const absentCount = list.filter((r) => r.status === "ABSENT").length;

    const workingDays = totalShifts - approvedLeaveCount;
    const attendanceRate = workingDays > 0
      ? ((onTimeCount / workingDays) * 100).toFixed(1)
      : "100.0";

    return {
      records: list,
      stats: {
        totalShifts,
        onTimeCount,
        lateOrEarlyCount: lateCount + earlyCount,
        approvedLeaveCount,
        absentCount,
        attendanceRate: `${attendanceRate}%`,
      },
    };
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
    const exportDate = new Date().toLocaleDateString("vi-VN");
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
