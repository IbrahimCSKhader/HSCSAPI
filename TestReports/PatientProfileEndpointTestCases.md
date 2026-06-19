# PatientProfile Endpoint Test Cases

Test run date: 2026-06-19

Base URL used for verification: `http://localhost:5151`

Authentication used: Patient JWT token for `patientprofile.patient@test.local`.

Test data injected:
- Test patient account, verified by registration code.
- One future appointment for the patient.
- Two medical files:
  - Low severity: `81000000-0000-0000-0000-000000000001`
  - High severity: `81000000-0000-0000-0000-000000000002`
- Three notifications.
- Two authorized member accounts.
- One temporary authorized-member relation.

Build result:
- `dotnet build` succeeded.
- Existing migration-name warnings remained; no build errors.

| # | Endpoint | Method | Expected | Actual | Result | Notes |
|---|---|---|---:|---:|---|---|
| 1 | `/api/patient-profile/dashboard` | GET | 200 | 200 | Passed | Dashboard returned patient counters, health overview, visits by clinic, and upcoming appointments. |
| 2 | `/api/patient-profile/notifications?status=all&page=1&pageSize=20` | GET | 200 | 200 | Passed | Returned paged notification list. |
| 3 | `/api/patient-profile/notifications/80000000-0000-0000-0000-000000000002/read` | PATCH | 200 | 200 | Passed | Marked one notification as read. |
| 4 | `/api/patient-profile/notifications/read-all` | PATCH | 200 | 200 | Passed | Marked remaining unread notifications as read. |
| 5 | `/api/patient-profile/medical-records?type=all&page=1&pageSize=20` | GET | 200 | 200 | Passed | Returned paged medical records. |
| 6 | `/api/patient-profile/medical-records/81000000-0000-0000-0000-000000000001` | GET | 200 | 200 | Passed | Returned low-severity medical record details. |
| 7 | `/api/patient-profile/medical-records/81000000-0000-0000-0000-000000000001/download` | GET | 200 | 200 | Passed | Downloaded the test file successfully, 44 bytes. |
| 8 | `/api/patient-profile/medical-records/81000000-0000-0000-0000-000000000002/download-requests` | POST | 201 | 201 | Passed | Created a pending download request for the high-severity file. |
| 9 | `/api/patient-profile/download-requests?status=all&page=1&pageSize=20` | GET | 200 | 200 | Passed | Returned paged download request list. |
| 10 | `/api/patient-profile/authorized-members` | GET | 200 | 200 | Passed | Returned authorized members for the patient. |
| 11 | `/api/patient-profile/authorized-members/82000000-0000-0000-0000-000000000001` | DELETE | 204 | 204 | Passed | Removed the injected authorized-member relation. |
| 12 | `/api/patient-profile/authorized-member-invites` | GET | 200 | 200 | Passed | Returned invite list. |
| 13 | `/api/patient-profile/authorized-member-invites` | POST | 201 | 201 | Passed | Created a pending invite for `patientprofile.invite.member@test.local`. |
| 14 | `/api/patient-profile/authorized-member-invites/{inviteId}` | DELETE | 204 | 204 | Passed | Cancelled the invite created in test case 13. |

Coverage notes:
- Message endpoints were intentionally not added or tested because the current domain model has no messaging tables.
- Prescription records currently return zero/empty results because the current domain model has no prescription table.
- The notification model currently has no timestamp/body fields, so the API returns the fields available in the existing model.
