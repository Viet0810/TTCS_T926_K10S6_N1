const BASE_URL = window.APP_CONFIG?.apiBaseUrl || "http://192.168.1.107:5024/api";

function getAuthHeader() {
  const token = localStorage.getItem("token");

  return {
    "Content-Type": "application/json",
    Authorization: token ? `Bearer ${token}` : "",
  };
}

async function handleResponse(response) {
  const data = await response.json().catch(() => ({}));

  if (!response.ok) {
    throw new Error(data.message || `Lỗi HTTP ${response.status}`);
  }

  return data;
}

const API = {
  login: async (credentials) => {
    const response = await fetch(`${BASE_URL}/auth/login`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(credentials),
    });

    return handleResponse(response);
  },

  requestPasswordReset: async (email) => {
    const response = await fetch(`${BASE_URL}/auth/forgot-password`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email }),
    });
    return handleResponse(response);
  },

  verifyPasswordResetCode: async (email, code) => {
    const response = await fetch(`${BASE_URL}/auth/verify-reset-code`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, code }),
    });
    return handleResponse(response);
  },

  resetPassword: async (payload) => {
    const response = await fetch(`${BASE_URL}/auth/reset-password`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });
    return handleResponse(response);
  },

  getUsers: async () => {
    const response = await fetch(`${BASE_URL}/users`, {
      method: "GET",
      headers: getAuthHeader(),
    });

    return handleResponse(response);
  },

  createUser: async (userData) => {
    const response = await fetch(`${BASE_URL}/users`, {
      method: "POST",
      headers: getAuthHeader(),
      body: JSON.stringify(userData),
    });

    return handleResponse(response);
  },

  updateUserRole: async (userId, role) => {
    const response = await fetch(`${BASE_URL}/users/${userId}/role`, {
      method: "PUT",
      headers: getAuthHeader(),
      body: JSON.stringify({ role }),
    });
    return handleResponse(response);
  },

  deleteUser: async (userId) => {
    const response = await fetch(`${BASE_URL}/users/${userId}`, {
      method: "DELETE",
      headers: getAuthHeader(),
    });

    return handleResponse(response);
  },
};
