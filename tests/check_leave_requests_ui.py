"""
Kiểm thử tự động tính toàn vẹn và các thành phần giao diện / backend của tính năng Đăng ký nghỉ phép (K10S6N1-101, K10S6N1-136, K10S6N1-137)
"""
import os
import re
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def test_backend_files():
    print("=== KIỂM TRA TỆP BACKEND ===")
    files = [
        "Backend/InternManagement/Infrastructure/LeaveRequestMigration.cs",
        "Backend/InternManagement/DTOs/LeaveRequestDtos.cs",
        "Backend/InternManagement/Services/ILeaveRequestService.cs",
        "Backend/InternManagement/Services/LeaveRequestService.cs",
        "Backend/InternManagement/Controllers/LeaveRequestsController.cs",
        "Backend/InternManagement.Tests/LeaveRequestChecks.cs"
    ]
    for rel_path in files:
        full_path = os.path.join(BASE_DIR, rel_path)
        assert os.path.exists(full_path), f"File {rel_path} không tồn tại!"
        with open(full_path, "r", encoding="utf-8") as f:
            content = f.read()
            assert len(content) > 100, f"File {rel_path} quá ngắn hoặc rỗng!"
        print(f"  [OK] {rel_path}")

def test_frontend_files():
    print("\n=== KIỂM TRA TỆP FRONTEND ===")
    files = [
        "Frontend/pages/intern-leave-request.html",
        "Frontend/css/leave-request.css",
        "Frontend/js/intern-leave-request.js"
    ]
    for rel_path in files:
        full_path = os.path.join(BASE_DIR, rel_path)
        assert os.path.exists(full_path), f"File {rel_path} không tồn tại!"
        with open(full_path, "r", encoding="utf-8") as f:
            content = f.read()
            assert len(content) > 100, f"File {rel_path} quá ngắn hoặc rỗng!"
        print(f"  [OK] {rel_path}")

def test_html_structure():
    print("\n=== KIỂM TRA CẤU TRÚC HTML ===")
    html_path = os.path.join(BASE_DIR, "Frontend/pages/intern-leave-request.html")
    with open(html_path, "r", encoding="utf-8") as f:
        html = f.read()

    # Form inputs
    assert 'id="leaveRequestForm"' in html, "Thiếu form id leaveRequestForm"
    assert 'id="leaveType"' in html, "Thiếu select leaveType"
    assert 'id="durationType"' in html, "Thiếu select durationType"
    assert 'id="startDate"' in html, "Thiếu input startDate"
    assert 'id="endDate"' in html, "Thiếu input endDate"
    assert 'id="reason"' in html, "Thiếu textarea reason"
    assert 'id="submitLeaveBtn"' in html, "Thiếu button submitLeaveBtn"

    # Stat cards
    assert 'id="totalLeaveCount"' in html, "Thiếu totalLeaveCount"
    assert 'id="pendingLeaveCount"' in html, "Thiếu pendingLeaveCount"
    assert 'id="approvedLeaveCount"' in html, "Thiếu approvedLeaveCount"
    assert 'id="rejectedLeaveCount"' in html, "Thiếu rejectedLeaveCount"

    # Table and modal
    assert 'id="leaveRequestsTableBody"' in html, "Thiếu tbody leaveRequestsTableBody"
    assert 'id="cancelModal"' in html, "Thiếu modal cancelModal"
    assert 'id="confirmCancelBtn"' in html, "Thiếu confirmCancelBtn"

    print("  [OK] Đầy đủ các phần tử Form, Thống kê, Bảng lịch sử và Modal xác nhận.")

def test_api_integration():
    print("\n=== KIỂM TRA TÍCH HỢP API.JS VÀ NAVIGATION.JS ===")
    api_path = os.path.join(BASE_DIR, "Frontend/js/api.js")
    with open(api_path, "r", encoding="utf-8") as f:
        api_code = f.read()

    assert "getLeaveRequests" in api_code, "Thiếu phương thức getLeaveRequests trong api.js"
    assert "createLeaveRequest" in api_code, "Thiếu phương thức createLeaveRequest trong api.js"
    assert "cancelLeaveRequest" in api_code, "Thiếu phương thức cancelLeaveRequest trong api.js"
    print("  [OK] api.js có đầy đủ 3 API getLeaveRequests, createLeaveRequest, cancelLeaveRequest.")

    nav_path = os.path.join(BASE_DIR, "Frontend/js/navigation.js")
    with open(nav_path, "r", encoding="utf-8") as f:
        nav_code = f.read()

    assert "intern-leave-request.html" in nav_code, "Thiếu mục điều hướng intern-leave-request.html trong navigation.js"
    print("  [OK] navigation.js đã tích hợp mục điều hướng 'Đăng ký nghỉ phép'.")

if __name__ == "__main__":
    try:
        test_backend_files()
        test_frontend_files()
        test_html_structure()
        test_api_integration()
        print("\n>>> TẤT CẢ CÁC BƯỚC KIỂM TRA ĐỀU THÀNH CÔNG (100% PASS) <<<")
    except AssertionError as e:
        print(f"\n[FAIL]: {e}")
        sys.exit(1)
