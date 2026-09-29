/**
 * Service Layer: Quản lý truy xuất dữ liệu Thực tập sinh cho màn hình HR
 * Lưu trữ trong LocalStorage, sẵn sàng chuyển USE_API = true khi nối Backend/CSDL
 */

const STORAGE_KEY = "intern_management_data_v1";

const InternService = {
  USE_API: false,
  API_BASE_URL: "https://localhost:7001/api",

  initStorage() {
    if (!localStorage.getItem(STORAGE_KEY)) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(INITIAL_INTERNS));
    }
  },

  getAllInterns() {
    this.initStorage();
    try {
      const data = localStorage.getItem(STORAGE_KEY);
      return data ? JSON.parse(data) : [...INITIAL_INTERNS];
    } catch (e) {
      return [...INITIAL_INTERNS];
    }
  },

  async getInterns(filters = {}) {
    if (this.USE_API) {
      const params = new URLSearchParams(filters);
      const res = await fetch(`${this.API_BASE_URL}/interns?${params}`);
      return await res.json();
    }

    const list = this.getAllInterns();
    return list.filter(item => {
      // 1. Tìm kiếm theo Tên, MSSV, Email, SĐT
      if (filters.search) {
        const kw = filters.search.trim().toLowerCase();
        const matchName = item.name.toLowerCase().includes(kw);
        const matchMssv = item.mssv.toLowerCase().includes(kw);
        const matchEmail = item.email.toLowerCase().includes(kw);
        const matchPhone = item.phone.toLowerCase().includes(kw);
        if (!matchName && !matchMssv && !matchEmail && !matchPhone) return false;
      }

      // 2. Lọc theo Trường đào tạo
      if (filters.school && item.school !== filters.school && item.schoolShort !== filters.school) {
        return false;
      }

      // 3. Lọc theo Chuyên ngành
      if (filters.major && item.major !== filters.major) {
        return false;
      }

      // 4. Lọc theo Trạng thái
      if (filters.status && item.status !== filters.status) {
        return false;
      }

      // 5. Lọc theo Đợt thực tập
      if (filters.batch && item.batch !== filters.batch) {
        return false;
      }

      // 6. Lọc theo Mentor
      if (filters.mentor && item.mentor !== filters.mentor) {
        return false;
      }

      return true;
    });
  },

  async getInternById(id) {
    const list = this.getAllInterns();
    return list.find(item => item.id === Number(id)) || null;
  },

  async addIntern(newIntern) {
    const list = this.getAllInterns();
    const id = list.length > 0 ? Math.max(...list.map(i => i.id)) + 1 : 1;
    const matchedSchool = MOCK_UNIVERSITIES.find(u => u.name === newIntern.school);
    const schoolShort = matchedSchool ? matchedSchool.shortName : "DH";

    const created = {
      ...newIntern,
      id,
      schoolShort,
      avatar: newIntern.avatar || `https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=150&auto=format&fit=crop&q=80`,
      progress: Number(newIntern.progress) || 0,
      createdAt: new Date().toISOString()
    };

    list.unshift(created);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    return created;
  },

  async updateIntern(id, updatedFields) {
    const list = this.getAllInterns();
    const index = list.findIndex(i => i.id === Number(id));
    if (index !== -1) {
      list[index] = { ...list[index], ...updatedFields };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
      return list[index];
    }
    return null;
  },

  async bulkUpdateStatus(ids, newStatus) {
    const list = this.getAllInterns();
    const idSet = new Set(ids.map(Number));
    list.forEach(item => {
      if (idSet.has(item.id)) {
        item.status = newStatus;
      }
    });
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    return true;
  },

  async bulkAssignMentor(ids, mentorName) {
    const list = this.getAllInterns();
    const idSet = new Set(ids.map(Number));
    list.forEach(item => {
      if (idSet.has(item.id)) {
        item.mentor = mentorName;
      }
    });
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    return true;
  },

  async deleteIntern(id) {
    let list = this.getAllInterns();
    list = list.filter(i => i.id !== Number(id));
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    return true;
  },

  getKPIStats() {
    const list = this.getAllInterns();
    return {
      total: list.length,
      active: list.filter(i => i.status === "Đang thực tập").length,
      pending: list.filter(i => i.status === "Chờ tiếp nhận" || i.status === "Đang phỏng vấn").length,
      completed: list.filter(i => i.status === "Đã hoàn thành").length
    };
  },

  exportCSV(interns) {
    const headers = ["ID", "Họ và Tên", "MSSV", "Email", "Số điện thoại", "Trường đào tạo", "Chuyên ngành", "Vị trí", "Đợt thực tập", "Người hướng dẫn", "Trạng thái", "Tiến độ (%)", "Điểm GPA"];
    const rows = interns.map(i => [
      i.id,
      `"${i.name}"`,
      `"${i.mssv}"`,
      `"${i.email}"`,
      `"${i.phone}"`,
      `"${i.school}"`,
      `"${i.major}"`,
      `"${i.role}"`,
      `"${i.batch}"`,
      `"${i.mentor || ''}"`,
      `"${i.status}"`,
      i.progress,
      `"${i.gpa}"`
    ]);

    const csvContent = "\uFEFF" + [headers.join(","), ...rows.map(e => e.join(","))].join("\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", `Danh_sach_thuc_tap_sinh_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }
};
