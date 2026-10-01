const form = document.getElementById("editUserForm");
const message = document.getElementById("editMessage");
const userId = new URLSearchParams(window.location.search).get("id");

function showMessage(text, type = "error") {
  message.textContent = text;
  message.className = `edit-message ${type}`;
}

document.getElementById("logoutBtn").addEventListener("click", () => {
  localStorage.removeItem("token");
  localStorage.removeItem("role");
  localStorage.removeItem("user");
  window.location.href = "../index.html";
});

document.getElementById("cancelEditBtn").addEventListener("click", () => {
  window.location.href = "user-manage.html";
});

async function loadUser() {
  if (!userId || !/^\d+$/.test(userId)) {
    showMessage("Mã tài khoản không hợp lệ.");
    return;
  }

  try {
    const user = await API.getUser(userId);
    document.getElementById("fullName").value = user.fullName;
    document.getElementById("email").value = user.email;
    document.getElementById("role").value = user.role;
    form.hidden = false;
  } catch (error) {
    showMessage(error.message);
  }
}

form.addEventListener("submit", async (event) => {
  event.preventDefault();
  const password = document.getElementById("password").value;

  try {
    const result = await API.updateUser(userId, {
      fullName: document.getElementById("fullName").value.trim(),
      email: document.getElementById("email").value.trim(),
      role: document.getElementById("role").value,
      password: password || null,
    });
    showMessage(result.message, "success");
    window.setTimeout(() => {
      window.location.href = "user-manage.html";
    }, 900);
  } catch (error) {
    showMessage(error.message);
  }
});

loadUser();
