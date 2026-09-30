const BASE_URL = "http://localhost:5024/api";

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

  getCurrentUser: async () => {
    const response = await fetch(`${BASE_URL}/auth/me`, {
      method: "GET",
      headers: getAuthHeader(),
    });

    return handleResponse(response);
  },

  getRolePermissions: async () => {
    const response = await fetch(`${BASE_URL}/auth/roles/permissions`, {
      method: "GET",
      headers: getAuthHeader(),
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

  deleteUser: async (userId) => {
    const response = await fetch(`${BASE_URL}/users/${userId}`, {
      method: "DELETE",
      headers: getAuthHeader(),
    });

    return handleResponse(response);
  },
};
