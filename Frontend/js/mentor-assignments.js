(() => {
  const base = window.APP_CONFIG?.apiBaseUrl || "http://localhost:5024/api";
  const token = localStorage.getItem("token");
  const headers = { "Content-Type": "application/json", Authorization: token ? `Bearer ${token}` : "" };
  const esc = value => String(value ?? "").replace(/[&<>"']/g, c => ({ "&":"&amp;", "<":"&lt;", ">":"&gt;", '"':"&quot;", "'":"&#39;" }[c]));
  async function api(path, options = {}) {
    const response = await fetch(`${base}${path}`, { ...options, headers: { ...headers, ...options.headers } });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(data.message || `HTTP ${response.status}`);
    return data;
  }
  async function load() {
    const data = await api("/mentor/assignment-options");
    const mentorSelect = document.getElementById("assignmentMentor");
    mentorSelect.innerHTML = '<option value="">Chọn Mentor</option>' + data.mentors.map(m => `<option value="${m.Id}">${esc(m.Name)} · ${esc(m.Email)}</option>`).join("");
    const assignments = new Map(data.assignments.map(a => [a.InternId, a.MentorName]));
    document.getElementById("assignmentRows").innerHTML = data.interns.map(i => `<tr><td><input type="checkbox" class="intern-assignment-check" value="${i.Id}" aria-label="Chọn ${esc(i.Name)}"></td><td>${esc(i.Name)}</td><td>${esc(i.Mssv)}</td><td>${esc(i.Email)}</td><td>${esc(assignments.get(i.Id) || "Chưa phân công")}</td></tr>`).join("") || '<tr><td colspan="5">Chưa có hồ sơ thực tập sinh.</td></tr>';
    document.getElementById("selectAllInterns").checked = false;
  }
  document.getElementById("assignmentForm").addEventListener("submit", async event => {
    event.preventDefault();
    const button = event.currentTarget.querySelector("button[type=submit]"); button.disabled = true;
    const message = document.getElementById("assignmentMessage"); message.textContent = "Đang lưu...";
    try {
      const internIds = [...document.querySelectorAll(".intern-assignment-check:checked")].map(input => Number(input.value));
      if (!internIds.length || !Number(document.getElementById("assignmentMentor").value)) throw new Error("Chọn ít nhất một thực tập sinh và một Mentor.");
      const result = await api("/mentor/assignments/bulk", { method: "POST", body: JSON.stringify({ internIds, mentorUserId: Number(document.getElementById("assignmentMentor").value) }) });
      message.textContent = result.message; await load();
    } catch (error) { message.textContent = error.message; }
    finally { button.disabled = false; }
  });
  document.getElementById("selectAllInterns").addEventListener("change", event => document.querySelectorAll(".intern-assignment-check").forEach(input => input.checked = event.target.checked));
  document.getElementById("logoutBtn").addEventListener("click", () => { localStorage.clear(); location.replace("../index.html"); });
  load().catch(error => { document.getElementById("assignmentMessage").textContent = error.message; });
})();
