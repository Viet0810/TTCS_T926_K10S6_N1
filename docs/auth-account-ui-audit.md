# Auth/account UI audit

Audit before UI changes; existing validation rules and API flows are preserved.

| Page / form | Fields | Previous placeholders | Previous input icons | Existing validation | UI changes needed |
| --- | --- | --- | --- | --- | --- |
| index.html / Login | Email, password | Short, already correct | Password eye | Shared email/password validation, blur/submit, inline errors | Yes: share input geometry |
| register.html | Name, email, password, confirmation, phone, school, major | Email examples, password policy, phone example, descriptive school/major | Password eyes | Shared validators and inline errors | Yes |
| index.html / Forgot Password | Email | Registered-email instruction | Left envelope | Native required/email; compact server message | Yes |
| reset-password.html | New password, confirmation | Minimum length; short confirmation | Password eyes | Shared password policy; confirmation on submit | Yes: inline field errors |
| pages/change-password.html | Current/new password, confirmation | Missing current/confirmation; policy in new password | Circle glyph toggles | Blur/submit, shared policy, same-password and confirmation checks, mapped server errors | Yes |
| pages/user-create.html | Name, email, password, confirmation, role | Email example; minimum length | None | Native required/email; existing password policy/confirmation for INTERN | Yes: eyes and inline password errors; retain generated HR/Mentor password flow |
| pages/user-manage.html | Search; account actions | Search prompt | No account input icons | Existing permission checks and compact messages | No |
| pages/profile.html | Email displayed as text; editable personal details | Long phone example; missing personal-detail prompts | None | Existing personal-detail validation | Yes: shared inputs and short prompts |
| pages/intern-manage.html | Profile email, mentor email | None | None | Existing inline profile validators | Yes: shared email input style and short prompts |

Verification: existing check_change_password_ui.py passed all 16 cases (four roles, login/change password, 390px/1440px). Further verification was blocked by Windows process launch error 1909.
