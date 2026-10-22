const InternProfile = {
  fields: [
    {
      "key": "fullName",
      "label": "Họ và tên",
      "type": "text",
      "max": 200,
      "required": true
    },
    {
      "key": "email",
      "label": "Email",
      "type": "email",
      "max": 254,
      "required": true
    },
    {
      "key": "phone",
      "label": "Số điện thoại",
      "type": "tel",
      "max": 20,
      "required": true
    },
    {
      "key": "school",
      "label": "Trường đào tạo",
      "type": "text",
      "max": 200,
      "required": true
    },
    {
      "key": "major",
      "label": "Chuyên ngành",
      "type": "text",
      "max": 200,
      "required": true
    },
    {
      "key": "studentCode",
      "label": "Mã sinh viên",
      "type": "text",
      "max": 50
    },
    {
      "key": "className",
      "label": "Lớp",
      "type": "text",
      "max": 100
    },
    {
      "key": "faculty",
      "label": "Khoa",
      "type": "text",
      "max": 200
    },
    {
      "key": "dateOfBirth",
      "label": "Ngày sinh",
      "type": "date",
      "max": null
    },
    {
      "key": "address",
      "label": "Địa chỉ liên hệ",
      "type": "textarea",
      "max": 500
    },
    {
      "key": "organization",
      "label": "Đơn vị thực tập",
      "type": "text",
      "max": 200
    },
    {
      "key": "organizationAddress",
      "label": "Địa chỉ đơn vị",
      "type": "textarea",
      "max": 500
    },
    {
      "key": "department",
      "label": "Bộ phận thực tập",
      "type": "text",
      "max": 200
    },
    {
      "key": "position",
      "label": "Vị trí thực tập",
      "type": "text",
      "max": 200
    },
    {
      "key": "mentor",
      "label": "Người hướng dẫn tại đơn vị",
      "type": "text",
      "max": 200
    },
    {
      "key": "mentorEmail",
      "label": "Email người hướng dẫn",
      "type": "email",
      "max": 254
    },
    {
      "key": "mentorPhone",
      "label": "Số điện thoại người hướng dẫn",
      "type": "tel",
      "max": 20
    },
    {
      "key": "academicSupervisor",
      "label": "Giảng viên hướng dẫn",
      "type": "text",
      "max": 200
    },
    {
      "key": "startDate",
      "label": "Ngày bắt đầu",
      "type": "date",
      "max": null
    },
    {
      "key": "endDate",
      "label": "Ngày kết thúc",
      "type": "date",
      "max": null
    },
    {
      "key": "status",
      "label": "Trạng thái thực tập",
      "type": "select",
      "max": 50
    },
    {
      "key": "internshipTopic",
      "label": "Đề tài / nội dung thực tập",
      "type": "textarea",
      "max": 500
    },
    {
      "key": "notes",
      "label": "Ghi chú",
      "type": "textarea",
      "max": 2000
    }
  ],
  groups: [
    {
      "title": "Thông tin cá nhân",
      "keys": [
        "fullName",
        "dateOfBirth",
        "email",
        "phone",
        "address"
      ]
    },
    {
      "title": "Thông tin đào tạo",
      "keys": [
        "studentCode",
        "className",
        "school",
        "faculty",
        "major"
      ]
    },
    {
      "title": "Thông tin thực tập",
      "keys": [
        "organization",
        "organizationAddress",
        "department",
        "position",
        "internshipTopic",
        "startDate",
        "endDate",
        "status"
      ]
    },
    {
      "title": "Người hướng dẫn và ghi chú",
      "keys": [
        "mentor",
        "mentorEmail",
        "mentorPhone",
        "academicSupervisor",
        "notes"
      ]
    }
  ],
  statuses: ["Chờ tiếp nhận", "Đang thực tập", "Đã hoàn thành", "Đã dừng"],
  statusBadge(status) {
    const tones = {"Chờ tiếp nhận":"pending", "Đang thực tập":"active", "Đã hoàn thành":"approved", "Đã dừng":"rejected"};
    const badge = document.createElement("span"); badge.className = `status-badge ${tones[status] || ""}`;
    badge.textContent = status || "Chưa xác định"; return badge;
  },
  format(value, key) {
    if (!value) return "Chưa bổ sung";
    if (["dateOfBirth", "startDate", "endDate"].includes(key)) return value.split("-").reverse().join("/");
    return String(value);
  },
  details(container, profile) {
    container.replaceChildren();
    for (const group of this.groups) {
      const section = document.createElement("section");
      const title = document.createElement("h3"); title.textContent = group.title;
      const list = document.createElement("dl"); list.className = "profile-details";
      for (const key of group.keys) {
        const field = this.fields.find((field) => field.key === key);
        const item = document.createElement("div"), label = document.createElement("dt"), value = document.createElement("dd");
        label.textContent = field.label;
        if (key === "status") value.append(this.statusBadge(profile[key]));
        else value.textContent = this.format(profile[key], key);
        item.append(label, value); list.appendChild(item);
      }
      section.append(title, list); container.appendChild(section);
    }
  },
  show(profile, onEdit = null) {
    document.getElementById("profileDialogTitle").textContent = profile.fullName;
    this.details(document.getElementById("profileDialogDetails"), profile);
    const dialog = document.getElementById("profileDialog");
    let edit = document.getElementById("editProfileDialog");
    if (!edit) {
      edit = document.createElement("button");
      edit.id = "editProfileDialog"; edit.type = "button"; edit.className = "btn-primary";
      edit.textContent = "Chỉnh sửa hồ sơ";
      document.getElementById("closeProfileDialog").before(edit);
    }
    edit.hidden = typeof onEdit !== "function";
    edit.onclick = onEdit ? () => { dialog.close(); onEdit(profile); } : null;
    dialog.showModal();
  }
};
document.addEventListener("click", (event) => {
  if (event.target.closest("#closeProfileDialog")) document.getElementById("profileDialog")?.close();
});
