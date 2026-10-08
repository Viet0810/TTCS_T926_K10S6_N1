async function initializeDashboard() {
  if (!localStorage.getItem("token")) {
    window.location.href = "../index.html";
    return;
  }

  try {
    const session = await API.getCurrentUser();
    const user = session.user;
    document.getElementById("roleBadge").textContent = user.role;
    const welcome = document.getElementById("welcomeTitle");
    if (welcome) welcome.textContent = `Xin chào, ${user.fullName}`;
  } catch (error) {
    const message = document.getElementById("dashboardMessage") || document.getElementById("programDateMessage");
    if (message) {
      message.textContent = error.message;
      message.hidden = false;
    }
    Session.redirectIfExpired(error);
  }
}


initializeDashboard();
