document.addEventListener("DOMContentLoaded", async () => {
  const result = document.getElementById("result");
  const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
  document.getElementById("logoutBtn").addEventListener("click", Session.logout);
  try {
    const session = await API.getCurrentUser();
    if (session.user?.role !== "INTERN") return location.replace("dashboard.html");
    const data = await API.getMyPublishedEvaluation();
    result.innerHTML = `<div class="published-result"><span class="status-badge status-published">Đã công bố</span><h3>${escapeHtml(data.studentCode || "")} · ${escapeHtml(data.fullName)}</h3><p>${escapeHtml(data.programName || "")}</p><strong class="result-score">${data.averageScore}/100</strong><h4>${escapeHtml(data.finalClassification)}</h4><p>${escapeHtml(data.aggregateComment)}</p><small>Công bố lúc ${new Date(data.publishedAt).toLocaleString("vi-VN")}</small></div>`;
  } catch (error) { result.innerHTML = `<p class="empty-state">${escapeHtml(error.status === 404 ? "Kết quả chưa được HR công bố." : error.message)}</p>`; }
});
