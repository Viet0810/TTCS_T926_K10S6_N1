// Thay đổi URL Backend của bạn tại đây
const BASE_URL = "http://localhost:5024/api";

// Hàm lấy Token đã lưu sau khi đăng nhập
function getAuthHeader() {
  const token = localStorage.getItem("token");
  return {
    "Content-Type": "application/json",
    Authorization: token ? `Bearer ${token}` : "",
  };
}

const API = {
  login: async (credentials) => {
    const res = await fetch(`${BASE_URL}/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(credentials),
    });
    return res.json();
  },

  getUsers: async () => {
    const res = await fetch(`${BASE_URL}/users`, {
      method: "GET",
      headers: getAuthHeader(),
    });
    return res.json();
  },

  createUser: async (userData) => {
    const res = await fetch(`${BASE_URL}/users`, {
      method: "POST",
      headers: getAuthHeader(),
      body: JSON.stringify(userData),
    });
    return res.json();
  },

  deleteUser: async (userId) => {
    const res = await fetch(`${BASE_URL}/users/${userId}`, {
      method: "DELETE",
      headers: getAuthHeader(),
    });
    return res.json();
  },
};
