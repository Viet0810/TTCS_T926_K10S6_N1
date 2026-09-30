async function loadProfile() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const session = await API.getCurrentUser();
    const user = session.user;
    document.getElementById("profileName").textContent = user.fullName;
    document.getElementById("profileEmail").textContent = user.email;
    document.getElementById("profileUsername").textContent = user.username;
    document.getElementById("profileRole").textContent = user.role;
    document.getElementById("profileRoleName").textContent = user.role;
  } catch (error) {
    document.getElementById("profileMessage").textContent = error.message;
    if (
      error.message.includes("đăng nhập") ||
      error.message.includes("phiên")
    ) {
      localStorage.clear();
      window.location.href = "../index.html";
    }
  }
}

document.getElementById("logoutBtn").addEventListener("click", () => {
  localStorage.clear();
  window.location.href = "../index.html";
});

loadProfile();
