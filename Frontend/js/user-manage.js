document
  .getElementById("createUserForm")
  .addEventListener("submit", async function (event) {
    event.preventDefault();

    const fullName = document.getElementById("fullName").value.trim();

    const email = document.getElementById("email").value.trim();

    const password = document.getElementById("password").value;

    const role = document.getElementById("role").value;

    try {
      const result = await API.createUser({
        fullName,
        email,
        password,
        role,
      });

      alert(result.message);

      this.reset();

      await loadUsers();
    } catch (error) {
      alert(error.message);
    }
  });
