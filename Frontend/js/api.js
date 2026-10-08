const BASE_URL = "http://localhost:5024/api";

class ApiError extends Error {
  constructor(message, status = 0, context = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.endpoint = (context.path || "").split(/[?#]/)[0];
    this.method = context.method || "GET";
    this.traceId = context.traceId || null;
    this.code = context.code || null;
    this.timestamp = new Date().toISOString();
    this.category = status === 0 ? "connection" : status === 401 ? "authentication"
      : status === 403 ? "permission" : status === 404 ? "not_found"
      : status === 409 ? "conflict" : status >= 500 ? "server"
      : status >= 200 && status < 300 ? "response" : "validation";
  }
}

function reportApiError(error) {
  // Metadata only: never print request bodies, headers, query strings or server exception text.
  console.error("[api.js] API request failed", {
    source: error.status ? "Backend" : "Frontend/connection",
    endpoint: error.endpoint, method: error.method, status: error.status,
    category: error.category, traceId: error.traceId, timestamp: error.timestamp,
    message: error.status === 0 ? "Không thể kết nối máy chủ."
      : error.category === "response" ? "Phản hồi máy chủ không hợp lệ." : defaultApiMessage(error.status),
  });
  return error;
}

async function requestApi(path, options = {}, responseType = "json") {
  const context = { path, method: options.method || "GET" };
  let response;
  try {
    response = await fetch(`${BASE_URL}${path}`, options);
  } catch (error) {
    if (error?.name === "AbortError") throw error;
    throw reportApiError(new ApiError("Không thể kết nối máy chủ. Vui lòng kiểm tra kết nối và thử lại.", 0, context));
  }
  context.traceId = response.headers?.get("X-Request-ID");
  if (!response.ok || responseType === "json") return handleResponse(response, context);
  try { return await response.blob(); }
  catch { throw reportApiError(new ApiError("Không thể tải nội dung tài liệu. Vui lòng thử lại.", response.status, context)); }
}

function getAuthHeader() {
  const token = localStorage.getItem("token");

  return {
    "Content-Type": "application/json",
    Authorization: token ? `Bearer ${token}` : "",
  };
}

function defaultApiMessage(status) {
  if (status === 401) return "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.";
  if (status === 403) return "Bạn không có quyền thực hiện chức năng này.";
  if (status === 404) return "Không tìm thấy dữ liệu yêu cầu.";
  if (status === 409) return "Dữ liệu đã thay đổi hoặc bị trùng. Vui lòng kiểm tra và thử lại.";
  if (status === 413) return "Tệp vượt quá dung lượng cho phép.";
  if (status >= 500) return "Máy chủ chưa thể xử lý yêu cầu. Vui lòng thử lại sau.";
  return `Yêu cầu không thành công (HTTP ${status}).`;
}

async function handleResponse(response, context = {}) {
  if (response.ok && response.status === 204) return null;
  let data;
  try { data = await response.json(); }
  catch {
    if (response.ok) throw reportApiError(new ApiError("Máy chủ trả dữ liệu không hợp lệ. Vui lòng thử lại sau.", response.status, context));
    data = {};
  }

  if (!response.ok) {
    if (data?.code === "PASSWORD_CHANGE_REQUIRED") Session.redirectToPasswordChange();
    const validation = data?.errors ? Object.values(data.errors).flat().join(" ") : "";
    const message = data?.message || validation || (response.status < 500 ? data?.detail : "") || defaultApiMessage(response.status);
    throw reportApiError(new ApiError(message, response.status, { ...context, code: data?.code }));
  }

  return data;
}

const Session = {
  redirectToPasswordChange() {
    if (!location.pathname.endsWith("/change-password.html"))
      location.replace(location.pathname.includes("/pages/") ? "change-password.html" : "pages/change-password.html");
  },
  clear() { ["token", "role", "user"].forEach((key) => localStorage.removeItem(key)); },
  logout() { Session.clear(); location.href = "../index.html"; },
  hasPermission(session, permission) { return session.permissions?.includes(permission) === true; },
  redirectIfExpired(error) { if (error.status !== 401) return false; Session.logout(); return true; },
};

function saveDownload(blob, fileName) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  try { link.click(); }
  finally {
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}

const API = {
  changePassword: (data) => requestApi("/account/change-password", { method: "PUT", headers: getAuthHeader(), body: JSON.stringify(data) }),
  resendLoginEmail: (id) => requestApi(`/users/${encodeURIComponent(id)}/resend-login-email`, { method: "POST", headers: getAuthHeader() }),
  getAssignments: () => requestApi("/intern-assignments", { headers: getAuthHeader() }),
  getAssignmentInterns: () => requestApi("/intern-assignments/interns", { headers: getAuthHeader() }),
  getAssignmentMentors: () => requestApi("/intern-assignments/mentors", { headers: getAuthHeader() }),
  getAssignmentPrograms: () => requestApi("/intern-assignments/programs", { headers: getAuthHeader() }),
  assignMentor: (internId, assignment) => requestApi(`/intern-assignments/${encodeURIComponent(internId)}`, {
    method: "PUT", headers: getAuthHeader(), body: JSON.stringify(assignment),
  }),
  getMySchedule: () => requestApi("/interns/me/schedule", { headers: getAuthHeader() }),
  login: (credentials) => requestApi("/auth/login", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(credentials),
  }),

  forgotPassword: (email) => requestApi("/auth/forgot-password", {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email })
  }),
  resetPassword: (token, password) => requestApi("/auth/reset-password", {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ token, password })
  }),
  getMyDocuments: () => requestApi("/interns/me/documents", { headers: getAuthHeader() }),
  uploadMyDocument: (kind, file) => {
    const body = new FormData(); body.append("file", file);
    return requestApi(`/interns/me/documents/${encodeURIComponent(kind)}`, {
      method: "PUT", headers: { Authorization: getAuthHeader().Authorization }, body
    });
  },
  downloadMyDocument: (kind) => requestApi(`/interns/me/documents/${encodeURIComponent(kind)}`, { headers: getAuthHeader() }, "blob"),
  getDocumentReviews: () => requestApi("/document-reviews", { headers: getAuthHeader() }),
  downloadDocumentReview: (id, kind) => requestApi(`/document-reviews/${encodeURIComponent(id)}/${encodeURIComponent(kind)}`, { headers: getAuthHeader() }, "blob"),
  reviewDocument: (id, kind, decision) => requestApi(`/document-reviews/${encodeURIComponent(id)}/${encodeURIComponent(kind)}`, {
    method: "PUT", headers: getAuthHeader(), body: JSON.stringify(decision),
  }),
  getInterns: () => requestApi("/interns", { headers: getAuthHeader() }),
  getMyInternProfile: () => requestApi("/interns/me", { headers: getAuthHeader() }),
  updateMyInternProfile: (profile) => requestApi("/interns/me", {
    method: "PUT", headers: getAuthHeader(), body: JSON.stringify(profile),
  }),
  createIntern: (intern) => requestApi("/interns", {
    method: "POST", headers: getAuthHeader(), body: JSON.stringify(intern)
  }),
  updateIntern: (id, intern) => requestApi(`/interns/${encodeURIComponent(id)}`, {
    method: "PUT", headers: getAuthHeader(), body: JSON.stringify(intern)
  }),

  getCurrentUser: async () => {
    const session = await requestApi("/auth/me", { headers: getAuthHeader() });
    if (session.user?.mustChangePassword && !location.pathname.endsWith("/change-password.html")) {
      Session.redirectToPasswordChange();
      throw new ApiError("Vui lòng đổi mật khẩu tạm trước khi sử dụng hệ thống.", 403, { code: "PASSWORD_CHANGE_REQUIRED" });
    }
    window.AppNavigation?.render(session);
    return session;
  },
  getRolePermissions: () => requestApi("/auth/roles/permissions", { headers: getAuthHeader() }),
  getUsers: () => requestApi("/users", { headers: getAuthHeader() }),
  createUser: (userData) => requestApi("/users", {
    method: "POST",
    headers: getAuthHeader(),
    body: JSON.stringify(userData),
  }),
  deleteUser: (userId) => requestApi(`/users/${encodeURIComponent(userId)}`, {
    method: "DELETE",
    headers: getAuthHeader(),
  }),
};
