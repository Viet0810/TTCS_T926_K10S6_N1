/**
 * Service Layer: Quản lý truy xuất dữ liệu Thực tập sinh cho màn hình HR
 * Đọc và cập nhật hồ sơ thực tập sinh qua Backend/CSDL.
 */

const STORAGE_KEY = "intern_management_data_v1";

const InternService = {
  USE_API: true,
  API_BASE_URL: window.APP_CONFIG?.apiBaseUrl || "http://localhost:5024/api",
  apiInterns: [],

  async apiRequest(path, options = {}) {
    const token = localStorage.getItem("token");
    const response = await fetch(`${this.API_BASE_URL}${path}`, {
      ...options,
      headers: { "Content-Type": "application/json", Authorization: token ? `Bearer ${token}` : "", ...options.headers }
    });
    const body = await response.json().catch(() => ({}));
    if (!response.ok) {
      const error = new Error(body.message || `Lỗi HTTP ${response.status}`);
      error.status = response.status;
      error.details = body.errors || {};
      throw error;
    }
    return body;
  },

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
      let list = await this.apiRequest("/interns");
      list = list.map(item => ({ ...item, batch: item.batch || "", schoolShort: "", avatar: "" }));
      this.apiInterns = list;
      return list.filter(item => {
        const search = (filters.search || "").toLowerCase();
        return (!search || [item.name, item.mssv, item.email, item.phone].some(value => (value || "").toLowerCase().includes(search)))
          && (!filters.school || item.school === filters.school)
          && (!filters.major || item.major === filters.major)
          && (!filters.status || item.status === filters.status)
          && (!filters.mentor || item.mentor === filters.mentor);
      });
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

  async getMentors() {
    if (!this.USE_API) return MOCK_MENTORS.map(item => item.name);
    return this.apiRequest("/interns/mentors");
  },

  async getInternById(id) {
    if (this.USE_API) {
      const item = await this.apiRequest(`/interns/${encodeURIComponent(id)}`);
      return { ...item, batch: item.batch || "", schoolShort: "", avatar: "" };
    }
    const list = this.getAllInterns();
    return list.find(item => item.id === Number(id)) || null;
  },

  async addIntern(newIntern) {
    if (this.USE_API) {
      const result = await this.apiRequest("/interns", {
        method: "POST",
        body: JSON.stringify({
          name: newIntern.name, mssv: newIntern.mssv, email: newIntern.email,
          phone: newIntern.phone, school: newIntern.school, major: newIntern.major,
          role: newIntern.role, status: newIntern.status, mentor: newIntern.mentor,
          progress: newIntern.progress, gpa: newIntern.gpa, department: newIntern.department,
          startDate: newIntern.startDate || null, endDate: newIntern.endDate || null
        })
      });
      return this.getInternById(result.id);
    }
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
    if (this.USE_API) {
      const updated = await this.apiRequest(`/interns/${encodeURIComponent(id)}`, {
        method: "PUT",
        body: JSON.stringify(updatedFields)
      });
      return updated.data;
    }
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

  async bulkAssignMentor(ids, mentorUserId) {
    if (this.USE_API) {
      const result = await this.apiRequest("/mentor/assignments/bulk", {
        method: "POST",
        body: JSON.stringify({ internIds: ids.map(Number), mentorUserId: Number(mentorUserId) })
      });
      await this.getInterns();
      return result;
    }
    const list = this.getAllInterns();
    const idSet = new Set(ids.map(Number));
    list.forEach(item => {
      if (idSet.has(item.id)) {
        item.mentor = mentorUserId;
      }
    });
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    return true;
  },

  async deleteIntern(id) {
    if (this.USE_API) {
      await this.apiRequest(`/interns/${encodeURIComponent(id)}`, { method: "DELETE" });
      return true;
    }
    let list = this.getAllInterns();
    list = list.filter(i => i.id !== Number(id));
    localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
    return true;
  },

  getKPIStats() {
    const list = this.USE_API ? this.apiInterns : this.getAllInterns();
    return {
      total: list.length,
      active: list.filter(i => i.status === "Đang thực tập").length,
      pending: list.filter(i => i.status === "Chờ tiếp nhận" || i.status === "Đang phỏng vấn").length,
      completed: list.filter(i => i.status === "Đã hoàn thành").length
    };
  },

  exportExcel(interns) {
    const exportDate = new Date().toLocaleDateString("vi-VN");

    const tableHtml = `
      <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
      <head>
        <meta http-equiv="content-type" content="application/vnd.ms-excel; charset=UTF-8">
        <!--[if gte mso 9]>
        <xml>
          <x:ExcelWorkbook>
            <x:ExcelWorksheets>
              <x:ExcelWorksheet>
                <x:Name>Thực Tập Sinh</x:Name>
                <x:WorksheetOptions><x:DisplayGridlines/></x:WorksheetOptions>
              </x:ExcelWorksheet>
            </x:ExcelWorksheets>
          </x:ExcelWorkbook>
        </xml>
        <![endif]-->
        <style>
          body { font-family: "Segoe UI", Arial, sans-serif; font-size: 13px; }
          table { border-collapse: collapse; width: 100%; }
          .title-row { font-size: 16pt; font-weight: bold; color: #1e1b4b; text-align: center; height: 45px; }
          .subtitle-row { font-size: 10pt; color: #64748b; font-style: italic; text-align: center; height: 25px; }
          th {
            background-color: #4338ca;
            color: #ffffff;
            font-weight: bold;
            text-align: center;
            border: 1px solid #312e81;
            padding: 10px 8px;
            height: 35px;
            font-size: 11pt;
          }
          td {
            border: 1px solid #cbd5e1;
            padding: 8px;
            font-size: 10pt;
            vertical-align: middle;
          }
          .center { text-align: center; }
          .bold { font-weight: bold; }
          .text-format { mso-number-format: "\\@"; } /* Giữ nguyên số 0 đầu cho SĐT và MSSV */
          .row-even { background-color: #f8fafc; }
        </style>
      </head>
      <body>
        <table>
          <tr>
            <td colspan="13" class="title-row" style="border:none; text-align:center;">
              BÁO CÁO DANH SÁCH THỰC TẬP SINH
            </td>
          </tr>
          <tr>
            <td colspan="13" class="subtitle-row" style="border:none; text-align:center;">
              Ngày xuất: ${exportDate} &bull; Tổng số: ${interns.length} thực tập sinh
            </td>
          </tr>
          <tr><td colspan="13" style="border:none; height:10px;"></td></tr>
          <thead>
            <tr>
              <th style="width: 50px;">STT</th>
              <th style="width: 180px;">Họ và Tên</th>
              <th style="width: 120px;">Mã số SV</th>
              <th style="width: 230px;">Email</th>
              <th style="width: 120px;">Số điện thoại</th>
              <th style="width: 250px;">Trường đào tạo</th>
              <th style="width: 180px;">Chuyên ngành</th>
              <th style="width: 160px;">Vị trí thực tập</th>
              <th style="width: 160px;">Đợt thực tập</th>
              <th style="width: 150px;">Người hướng dẫn</th>
              <th style="width: 130px;">Trạng thái</th>
              <th style="width: 90px;">Tiến độ</th>
              <th style="width: 90px;">Điểm GPA</th>
            </tr>
          </thead>
          <tbody>
            ${interns.map((i, idx) => `
              <tr class="${idx % 2 === 1 ? 'row-even' : ''}">
                <td class="center bold">${idx + 1}</td>
                <td class="bold">${i.name}</td>
                <td class="center text-format">${i.mssv}</td>
                <td>${i.email}</td>
                <td class="center text-format">${i.phone}</td>
                <td>${i.school}</td>
                <td>${i.major}</td>
                <td>${i.role}</td>
                <td class="center">${i.batch}</td>
                <td>${i.mentor || 'Chưa phân công'}</td>
                <td class="center">${i.status}</td>
                <td class="center bold">${i.progress}%</td>
                <td class="center bold">${i.gpa}</td>
              </tr>
            `).join('')}
          </tbody>
        </table>
      </body>
      </html>
    `;

    const blob = new Blob(["\uFEFF" + tableHtml], { type: "application/vnd.ms-excel;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", `Danh_sach_thuc_tap_sinh_${new Date().toISOString().slice(0, 10)}.xls`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  },

  exportCSV(interns) {
    this.exportExcel(interns);
  }
};
