document.addEventListener("DOMContentLoaded", function () {

  // =====================================================
  // THÊM MENU "QUẢN LÝ CHƯƠNG TRÌNH" CHO HR
  // =====================================================

  const roleMenu = document.getElementById("roleMenu");
  const roleBadge = document.getElementById("roleBadge");

  function addProgramSettingMenu() {
    if (!roleMenu || !roleBadge) {
      return;
    }

    const role = roleBadge.textContent
      .trim()
      .toUpperCase();

    // Chỉ HR mới được nhìn thấy chức năng này
    if (role !== "HR") {
      return;
    }

    // Không thêm trùng menu
    if (
      document.getElementById(
        "programSettingMenuLink"
      )
    ) {
      return;
    }

    const link = document.createElement("a");

    link.id = "programSettingMenuLink";
    link.href = "program-setting.html";
    link.textContent = "Quản lý chương trình";

    roleMenu.appendChild(link);
  }

  // Thử thêm ngay
  addProgramSettingMenu();

  // dashboard.js có thể cập nhật role sau khi gọi API
  if (roleBadge) {
    const observer = new MutationObserver(function () {
      addProgramSettingMenu();
    });

    observer.observe(roleBadge, {
      childList: true,
      subtree: true,
      characterData: true
    });
  }


  // =====================================================
  // FORM QUẢN LÝ THỜI GIAN
  // =====================================================

  const form = document.getElementById("programDateForm");

  /*
   * dashboard.html không có form.
   * Khi đó phần menu phía trên vẫn chạy,
   * còn phần xử lý ngày sẽ dừng ở đây.
   */
  if (!form) {
    return;
  }


  const startDateInput =
    document.getElementById("programStartDate");

  const endDateInput =
    document.getElementById("programEndDate");

  const durationElement =
    document.getElementById("programDuration");

  const statusElement =
    document.getElementById("programStatus");

  const messageElement =
    document.getElementById("programDateMessage");

  const saveButton =
    document.getElementById("programSaveBtn");

  const cancelButton =
    document.getElementById("programCancelBtn");

  const historyBody =
    document.getElementById("programHistoryBody");

  const emptyHistory =
    document.getElementById("emptyProgramHistory");


  const HISTORY_KEY =
    "internshipProgramHistory";

  const OLD_KEY =
    "internshipProgramTime";


  // =====================================================
  // THÔNG BÁO
  // =====================================================

  function showMessage(message, type) {
    messageElement.textContent = message;

    messageElement.className =
      `program-date-message ${type}`;
  }

  function clearMessage() {
    messageElement.textContent = "";

    messageElement.className =
      "program-date-message";
  }


  // =====================================================
  // TÍNH SỐ NGÀY
  // =====================================================

  function getDuration(startValue, endValue) {
    if (!startValue || !endValue) {
      return 0;
    }

    const start =
      new Date(startValue + "T00:00:00");

    const end =
      new Date(endValue + "T00:00:00");

    if (end < start) {
      return 0;
    }

    return (
      Math.floor(
        (end.getTime() - start.getTime()) /
        (1000 * 60 * 60 * 24)
      ) + 1
    );
  }


  function calculateDuration() {
    const days = getDuration(
      startDateInput.value,
      endDateInput.value
    );

    durationElement.textContent =
      days > 0
        ? `${days} ngày`
        : "Chưa xác định";
  }


  // =====================================================
  // TRẠNG THÁI CHƯƠNG TRÌNH
  // =====================================================

  function getProgramStatus(
    startValue,
    endValue
  ) {
    const today = new Date();

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
        className: "history-upcoming"
      };
    }


    if (today > end) {
      return {
        text: "Đã kết thúc",
        className: "history-ended"
      };
    }


    return {
      text: "Đang diễn ra",
      className: "history-active"
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
      status.text === "Sắp diễn ra"
    ) {
      statusElement.className =
        "program-status status-upcoming";
    }

    else if (
      status.text === "Đang diễn ra"
    ) {
      statusElement.className =
        "program-status status-active";
    }

    else {
      statusElement.className =
        "program-status status-ended";
    }
  }


  // =====================================================
  // KIỂM TRA DỮ LIỆU
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
        "Ngày kết thúc phải bằng hoặc sau ngày bắt đầu.",
        "error"
      );

      endDateInput.focus();

      return false;
    }


    return true;
  }


  // =====================================================
  // FORMAT NGÀY
  // =====================================================

  function formatDate(dateValue) {
    if (!dateValue) {
      return "";
    }

    const [year, month, day] =
      dateValue.split("-");

    return `${day}/${month}/${year}`;
  }


  // =====================================================
  // LOCAL STORAGE
  // =====================================================

  function getHistory() {
    try {
      const raw =
        localStorage.getItem(
          HISTORY_KEY
        );

      return raw
        ? JSON.parse(raw)
        : [];

    } catch (error) {

      console.error(
        "Lỗi đọc danh sách chương trình:",
        error
      );

      return [];
    }
  }


  function saveHistory(history) {
    localStorage.setItem(
      HISTORY_KEY,
      JSON.stringify(history)
    );
  }


  // =====================================================
  // CHUYỂN DỮ LIỆU CŨ
  // =====================================================

  function migrateOldData() {
    const oldRaw =
      localStorage.getItem(
        OLD_KEY
      );


    if (!oldRaw) {
      return;
    }


    try {
      const oldData =
        JSON.parse(oldRaw);


      if (
        !oldData.startDate ||
        !oldData.endDate
      ) {
        return;
      }


      const history =
        getHistory();


      const alreadyExists =
        history.some(function (item) {
          return (
            item.startDate ===
              oldData.startDate &&
            item.endDate ===
              oldData.endDate
          );
        });


      if (!alreadyExists) {
        history.push({
          startDate:
            oldData.startDate,

          endDate:
            oldData.endDate,

          createdAt:
            new Date().toISOString()
        });


        saveHistory(history);
      }


      localStorage.removeItem(
        OLD_KEY
      );


    } catch (error) {
      console.error(
        "Không thể chuyển dữ liệu cũ:",
        error
      );
    }
  }


  // =====================================================
  // HIỂN THỊ DANH SÁCH
  // =====================================================

  function renderHistory() {
    const history =
      getHistory();


    historyBody.innerHTML = "";


    if (history.length === 0) {
      emptyHistory.style.display =
        "block";

      return;
    }


    emptyHistory.style.display =
      "none";


    history.forEach(
      function (item, index) {

        const duration =
          getDuration(
            item.startDate,
            item.endDate
          );


        const status =
          getProgramStatus(
            item.startDate,
            item.endDate
          );


        const row =
          document.createElement("tr");


        row.innerHTML = `
          <td>${index + 1}</td>

          <td>
            ${formatDate(item.startDate)}
          </td>

          <td>
            ${formatDate(item.endDate)}
          </td>

          <td>
            ${duration} ngày
          </td>

          <td>
            <span
              class="history-status ${status.className}"
            >
              ${status.text}
            </span>
          </td>

          <td>
            <button
              type="button"
              class="btn-delete-program"
              data-index="${index}"
            >
              Xóa
            </button>
          </td>
        `;


        historyBody.appendChild(row);
      }
    );
  }


  // =====================================================
  // THAY ĐỔI NGÀY BẮT ĐẦU
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
          endDateInput.value = "";
        }
      }


      clearMessage();

      calculateDuration();

      updateCurrentStatus();
    }
  );


  // =====================================================
  // THAY ĐỔI NGÀY KẾT THÚC
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
  // LƯU CHƯƠNG TRÌNH
  // =====================================================

  form.addEventListener(
    "submit",
    function (event) {

      event.preventDefault();


      if (!validateDates()) {
        return;
      }


      saveButton.disabled = true;

      saveButton.textContent =
        "Đang lưu...";


      try {

        const history =
          getHistory();


        history.push({
          startDate:
            startDateInput.value,

          endDate:
            endDateInput.value,

          createdAt:
            new Date().toISOString()
        });


        saveHistory(history);


        renderHistory();


        showMessage(
          "Lưu thời gian chương trình thành công.",
          "success"
        );


        // Xóa dữ liệu form
        // sau khi lưu thành công

        startDateInput.value = "";

        endDateInput.value = "";

        endDateInput.removeAttribute(
          "min"
        );


        calculateDuration();

        updateCurrentStatus();


      } catch (error) {

        console.error(error);


        showMessage(
          "Không thể lưu thời gian chương trình.",
          "error"
        );


      } finally {

        saveButton.disabled = false;

        saveButton.textContent =
          "Lưu thời gian";
      }
    }
  );


  // =====================================================
  // HỦY
  // =====================================================

  cancelButton.addEventListener(
    "click",
    function () {

      startDateInput.value = "";

      endDateInput.value = "";


      endDateInput.removeAttribute(
        "min"
      );


      clearMessage();

      calculateDuration();

      updateCurrentStatus();
    }
  );


  // =====================================================
  // XÓA CHƯƠNG TRÌNH TRONG DANH SÁCH
  // =====================================================

  historyBody.addEventListener(
    "click",
    function (event) {

      if (
        !event.target.classList.contains(
          "btn-delete-program"
        )
      ) {
        return;
      }


      const index =
        Number(
          event.target.dataset.index
        );


      const history =
        getHistory();


      history.splice(
        index,
        1
      );


      saveHistory(history);


      renderHistory();


      showMessage(
        "Đã xóa chương trình.",
        "success"
      );
    }
  );


  // =====================================================
  // KHỞI TẠO
  // =====================================================

  migrateOldData();

  calculateDuration();

  updateCurrentStatus();

  renderHistory();

});