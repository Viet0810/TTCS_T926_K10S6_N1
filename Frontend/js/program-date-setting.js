document.addEventListener("DOMContentLoaded", function () {
  const API_PATH = "/program-schedule";

  // =====================================================
  // MENU QUẢN LÝ CHƯƠNG TRÌNH CHO HR
  // =====================================================

  const form =
    document.getElementById(
      "programDateForm"
    );

  if (!form) {
    return;
  }

  const startDateInput =
    document.getElementById(
      "programStartDate"
    );

  const endDateInput =
    document.getElementById(
      "programEndDate"
    );

  const durationElement =
    document.getElementById(
      "programDuration"
    );

  const statusElement =
    document.getElementById(
      "programStatus"
    );

  const messageElement =
    document.getElementById(
      "programDateMessage"
    );

  const saveButton =
    document.getElementById(
      "programSaveBtn"
    );

  const cancelButton =
    document.getElementById(
      "programCancelBtn"
    );

  const historyBody =
    document.getElementById(
      "programHistoryBody"
    );

  const emptyHistory =
    document.getElementById(
      "emptyProgramHistory"
    );

  // =====================================================
  // THÔNG BÁO
  // =====================================================

  function showMessage(
    message,
    type
  ) {
    messageElement.textContent =
      message;

    messageElement.className =
      `program-date-message ${type}`;
  }

  function clearMessage() {
    messageElement.textContent = "";

    messageElement.className =
      "program-date-message";
  }

  // =====================================================
  // XỬ LÝ NGÀY
  // =====================================================

  function getDuration(
    startValue,
    endValue
  ) {
    if (!startValue || !endValue) {
      return 0;
    }

    const start =
      new Date(
        startValue + "T00:00:00"
      );

    const end =
      new Date(
        endValue + "T00:00:00"
      );

    if (end < start) {
      return 0;
    }

    return (
      Math.floor(
        (
          end.getTime() -
          start.getTime()
        ) /
        (1000 * 60 * 60 * 24)
      ) + 1
    );
  }

  function calculateDuration() {
    const days =
      getDuration(
        startDateInput.value,
        endDateInput.value
      );

    durationElement.textContent =
      days > 0
        ? `${days} ngày`
        : "Chưa xác định";
  }

  // =====================================================
  // TRẠNG THÁI
  // =====================================================

  function getProgramStatus(
    startValue,
    endValue
  ) {
    const today =
      new Date();

    today.setHours(
      0,
      0,
      0,
      0
    );

    const start =
      new Date(
        startValue + "T00:00:00"
      );

    const end =
      new Date(
        endValue + "T23:59:59"
      );

    if (today < start) {
      return {
        text: "Sắp diễn ra",
        css: "history-upcoming"
      };
    }

    if (today > end) {
      return {
        text: "Đã kết thúc",
        css: "history-ended"
      };
    }

    return {
      text: "Đang diễn ra",
      css: "history-active"
    };
  }

  function updateCurrentStatus() {
    if (
      !startDateInput.value ||
      !endDateInput.value
    ) {
      statusElement.textContent =
        "Chưa thiết lập";

      statusElement.className =
        "program-status status-empty";

      return;
    }

    const status =
      getProgramStatus(
        startDateInput.value,
        endDateInput.value
      );

    statusElement.textContent =
      status.text;

    if (
      status.text ===
      "Sắp diễn ra"
    ) {
      statusElement.className =
        "program-status status-upcoming";
    } else if (
      status.text ===
      "Đang diễn ra"
    ) {
      statusElement.className =
        "program-status status-active";
    } else {
      statusElement.className =
        "program-status status-ended";
    }
  }

  // =====================================================
  // VALIDATE
  // =====================================================

  function validateDates() {
    clearMessage();

    if (!startDateInput.value) {
      showMessage(
        "Vui lòng chọn ngày bắt đầu.",
        "error"
      );

      startDateInput.focus();

      return false;
    }

    if (!endDateInput.value) {
      showMessage(
        "Vui lòng chọn ngày kết thúc.",
        "error"
      );

      endDateInput.focus();

      return false;
    }

    if (
      endDateInput.value <
      startDateInput.value
    ) {
      showMessage(
        "Ngày kết thúc không được trước ngày bắt đầu.",
        "error"
      );

      endDateInput.focus();

      return false;
    }

    return true;
  }

  // =====================================================
  // FORMAT DATE
  // =====================================================

  function formatDate(dateValue) {
    if (!dateValue) {
      return "";
    }

    const dateOnly =
      dateValue.split("T")[0];

    const [
      year,
      month,
      day
    ] = dateOnly.split("-");

    return `${day}/${month}/${year}`;
  }

  function toInputDate(dateValue) {
    if (!dateValue) {
      return "";
    }

    return dateValue.split("T")[0];
  }

  // =====================================================
  // HIỂN THỊ DANH SÁCH
  // =====================================================

  function renderPrograms(programs) {
    historyBody.innerHTML = "";

    if (
      !Array.isArray(programs) ||
      programs.length === 0
    ) {
      emptyHistory.style.display =
        "block";

      return;
    }

    emptyHistory.style.display =
      "none";

    programs.forEach(
      function (program, index) {
        const startDate =
          toInputDate(
            program.startDate
          );

        const endDate =
          toInputDate(
            program.endDate
          );

        const status =
          getProgramStatus(
            startDate,
            endDate
          );

        const duration =
          program.durationDays ??
          getDuration(
            startDate,
            endDate
          );

        const row =
          document.createElement("tr");

        row.innerHTML = `
          <td>
            ${index + 1}
          </td>

          <td>
            ${formatDate(program.startDate)}
          </td>

          <td>
            ${formatDate(program.endDate)}
          </td>

          <td>
            ${duration} ngày
          </td>

          <td>
            <span
              class="history-status ${status.css}"
            >
              ${status.text}
            </span>
          </td>

          <td>
            <button
              type="button"
              class="btn-delete-program"
              data-id="${program.id}"
            >
              Xóa
            </button>
          </td>
        `;
        row.insertCell(1).textContent = program.name || `Chương trình #${program.id}`;
        row.insertCell(2).textContent = program.department || "Chưa bổ sung";

        historyBody.appendChild(
          row
        );
      }
    );
  }

  // =====================================================
  // GET DANH SÁCH TỪ BACKEND
  // =====================================================

  async function loadPrograms() {
    emptyHistory.textContent = "Đang tải chương trình…";
    emptyHistory.style.display = "block";
    try {
      const result = await requestApi(API_PATH, { headers: getAuthHeader() });

      renderPrograms(
        result.data
      );
      emptyHistory.textContent = "Chưa có thời gian chương trình nào được thiết lập.";

    } catch (error) {
      if (Session.redirectIfExpired(error)) return;
      console.error(
        "GET program schedules:",
        error
      );

      showMessage(
        error.message ||
        "Không thể tải dữ liệu chương trình từ máy chủ.",
        "error"
      );

      renderPrograms([]);
      emptyHistory.textContent = "Chưa thể tải danh sách chương trình.";
    }
  }

  // =====================================================
  // CHANGE START DATE
  // =====================================================

  startDateInput.addEventListener(
    "change",
    function () {
      if (startDateInput.value) {
        endDateInput.min =
          startDateInput.value;

        if (
          endDateInput.value &&
          endDateInput.value <
            startDateInput.value
        ) {
          endDateInput.value =
            "";
        }
      }

      clearMessage();

      calculateDuration();

      updateCurrentStatus();
    }
  );

  // =====================================================
  // CHANGE END DATE
  // =====================================================

  endDateInput.addEventListener(
    "change",
    function () {
      clearMessage();

      calculateDuration();

      updateCurrentStatus();
    }
  );

  // =====================================================
  // POST - THÊM CHƯƠNG TRÌNH
  // =====================================================

  form.addEventListener(
    "submit",
    async function (event) {
      event.preventDefault();

      if (saveButton.disabled || !validateDates()) {
        return;
      }

      saveButton.disabled =
        true;

      saveButton.textContent =
        "Đang lưu...";

      try {
        const requestData = {
          name: document.getElementById("programName").value.trim() || null,
          department: document.getElementById("programDepartment").value.trim() || null,
          startDate:
            startDateInput.value,

          endDate:
            endDateInput.value
        };

        const result = await requestApi(API_PATH, {
          method: "POST", headers: getAuthHeader(), body: JSON.stringify(requestData)
        });

        showMessage(
          result.message ||
          "Lưu thời gian chương trình thành công.",
          "success"
        );

        startDateInput.value =
          "";

        endDateInput.value =
          "";

        endDateInput.removeAttribute(
          "min"
        );

        calculateDuration();

        updateCurrentStatus();

        // Load lại toàn bộ danh sách
        await loadPrograms();

      } catch (error) {
      if (Session.redirectIfExpired(error)) return;
        console.error(
          "POST program schedule:",
          error
        );

        showMessage(
          error.message ||
          "Không thể lưu thời gian chương trình.",
          "error"
        );

      } finally {
        saveButton.disabled =
          false;

        saveButton.textContent =
          "Lưu thời gian";
      }
    }
  );

  // =====================================================
  // DELETE
  // =====================================================

  historyBody.addEventListener(
    "click",
    async function (event) {
      const deleteButton =
        event.target.closest(
          ".btn-delete-program"
        );

      if (!deleteButton) {
        return;
      }

      const id =
        deleteButton.dataset.id;

      if (!id) {
        return;
      }

      const confirmed =
        confirm(
          "Bạn có chắc muốn xóa thời gian chương trình này không?"
        );

      if (!confirmed) {
        return;
      }

      deleteButton.disabled =
        true;

      deleteButton.textContent =
        "Đang xóa...";

      try {
        const result = await requestApi(`${API_PATH}/${encodeURIComponent(id)}`, {
          method: "DELETE", headers: getAuthHeader()
        }) || {};

        showMessage(
          result.message ||
          "Xóa chương trình thành công.",
          "success"
        );

        await loadPrograms();

      } catch (error) {
      if (Session.redirectIfExpired(error)) return;
        console.error(
          "DELETE program schedule:",
          error
        );

        showMessage(
          error.message ||
          "Không thể xóa chương trình.",
          "error"
        );

        deleteButton.disabled =
          false;

        deleteButton.textContent =
          "Xóa";
      }
    }
  );

  // =====================================================
  // HỦY
  // =====================================================

  cancelButton.addEventListener(
    "click",
    function () {
      document.getElementById("programName").value = "";
      document.getElementById("programDepartment").value = "";
      startDateInput.value =
        "";

      endDateInput.value =
        "";

      endDateInput.removeAttribute(
        "min"
      );

      clearMessage();

      calculateDuration();

      updateCurrentStatus();
    }
  );

  // =====================================================
  // KHỞI TẠO
  // =====================================================

  calculateDuration();

  updateCurrentStatus();

  loadPrograms();
});
