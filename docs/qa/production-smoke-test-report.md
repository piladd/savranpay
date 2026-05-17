# SavranPay production smoke/e2e audit

Дата проверки: 2026-05-15  
Frontend: https://savranpay-5yge.vercel.app  
Backend API: https://savranpay-production.up.railway.app  
Ожидаемая связка: Vercel frontend -> Railway backend API -> Railway PostgreSQL  
Итоговый статус: **PASS WITH NOTES**

## 1. Проверенные URL

| URL | Результат |
|---|---|
| https://savranpay-5yge.vercel.app | 200 OK |
| https://savranpay-5yge.vercel.app/cabinet/client | 200 OK, SPA route |
| https://savranpay-production.up.railway.app/health/ready | 200 OK, `{"status":"ready","storage":"postgresql"}` |
| https://savranpay-production.up.railway.app/swagger | 200 OK |
| https://savranpay-production.up.railway.app/swagger/v1/swagger.json | 404, non-critical |
| OPTIONS https://savranpay-production.up.railway.app/api/v1/auth/login | 204, CORS OK for `https://savranpay-5yge.vercel.app` |

CORS preflight headers:

| Header | Value |
|---|---|
| `Access-Control-Allow-Origin` | `https://savranpay-5yge.vercel.app` |
| `Access-Control-Allow-Methods` | `GET,POST,PUT,PATCH,DELETE,OPTIONS` |
| `Access-Control-Allow-Headers` | `Content-Type,Authorization,Idempotency-Key,X-Request-Id` |

## 2. Подтверждение production API binding

Browser/Playwright network checks after Customer login confirmed API calls to Railway:

| Endpoint | Method | Status | Host |
|---|---:|---:|---|
| `/api/v1/auth/login` | POST | 200 | `savranpay-production.up.railway.app` |
| `/api/v1/dashboard` | GET | 200 | `savranpay-production.up.railway.app` |
| `/api/v1/auth/sessions` | GET | 200 | `savranpay-production.up.railway.app` |
| `/api/v1/auth/logout` | POST | 204 | `savranpay-production.up.railway.app` |

No production auth/dashboard requests were observed to `https://savranpay-5yge.vercel.app/api/v1/*`.

## QA-001 verification

| Item | Result |
|---|---|
| Было | Frontend использовал Vercel `/api/v1/*` demo-router |
| Стало | Frontend использует Railway backend API |
| Проверено | `/api/v1/auth/login`, `/api/v1/dashboard`, `/api/v1/auth/sessions`, `/api/v1/auth/logout` |
| Итог | **Fixed / Resolved** |

## 3. Проверенные учетные записи

| Роль | Email | Login | Auth/me | Sessions | Refresh | Logout | Protected route after logout |
|---|---|---:|---:|---:|---:|---:|---|
| Customer | `client@savranpay.local` | 200 | 200 | 200 | 200 | 204 | Login form shown |
| SupportOperator | `support@savranpay.local` | 200 | 200 | 200 | 200 | 204 | Login form shown |
| AmlOfficer | `aml@savranpay.local` | 200 | 200 | 200 | 200 | 204 | Login form shown |
| FraudOfficer | `fraud@savranpay.local` | 200 | 200 | 200 | 200 | 204 | Login form shown |
| Admin | `admin@savranpay.local` | 200 | 200 | 200 | 200 | 204 | Login form shown |
| Auditor | `audit@savranpay.local` | 200 | 200 | 200 | 200 | 204 | Login form shown |

Passwords were used only for runtime checks and were not written to repo artifacts.

## 4. Таблица ролей и доступных кабинетов

| Роль | `/cabinet/client` | `/cabinet/support` | `/cabinet/aml` | `/cabinet/fraud` | `/cabinet/admin` | `/cabinet/audit` |
|---|---|---|---|---|---|---|
| Customer | OK | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI |
| SupportOperator | Forbidden UI | OK | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI |
| AmlOfficer | Forbidden UI | Forbidden UI | OK | Forbidden UI | Forbidden UI | Forbidden UI |
| FraudOfficer | Forbidden UI | Forbidden UI | Forbidden UI | OK | Forbidden UI | Forbidden UI |
| Admin | OK | OK | OK | OK | OK | OK |
| Auditor | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI | OK |

No white screen, infinite loader, or critical console error was observed in the checked role routes. Expected forbidden states rendered correctly. Playwright had intermittent page/connection timeouts during one broad matrix run; targeted reruns for SupportOperator and Admin passed.

## 5. Таблица проверенных кнопок

| Роль | Страница | Кнопка/действие | Результат | HTTP status | Console error | Комментарий |
|---|---|---|---|---|---|---|
| Guest | Login | Sidebar cabinet items | OK | N/A | No | Navigation changes protected route state |
| Guest | Login | `Войти` | OK | 200 | No | Calls Railway `auth/login` |
| Customer | Client | Show/hide account number | OK | N/A | No | UI state toggles |
| Customer | Client | Refresh sessions | OK | 200 | No | Calls Railway `auth/sessions` |
| Customer | Client | Create transfer | OK | 202 | No | Created QA transfer `c2ab2878-b022-4cf0-a66d-e3d1bf44590c` |
| Customer | Client | Sign and confirm | OK | 200 | No | QA transfer reached `Settled` |
| Customer | Client | Open transfer details | OK | N/A | No | Details visible |
| Customer | Client | Repeat/dispute/cancel | Partially checked | N/A | No | Dangerous/status-changing actions only allowed on QA transfer; no real data touched |
| Customer | Client | Change password | Skipped | N/A | No | Not executed: destructive action for seed credentials |
| SupportOperator | Support | Search/filter | OK | N/A | No | UI usable |
| SupportOperator | Support | Open transfer/card | OK | 200 | No | Targeted UI rerun passed |
| SupportOperator | Support | Save support claim | OK | 200 | No | Executed only on QA transfer |
| AML | AML | Search/filter | OK | N/A | No | UI usable |
| AML | AML | Allow | OK | 202 | No | Executed only on QA transfer |
| AML | AML | ManualReview | Visible | N/A | No | Not needed after Allow check |
| AML | AML | Block | Skipped | N/A | No | Not executed: destructive action unless disposable entity is required |
| AML | AML | Request documents | UI-only / no backend action observed | N/A | No | Recorded as note |
| Fraud | Fraud | Search/filter | OK | N/A | No | UI usable |
| Fraud | Fraud | ManualReview / Step-up | OK | 202 | No | Executed only on QA transfer |
| Fraud | Fraud | Allow | Visible | N/A | No | Not needed after ManualReview check |
| Fraud | Fraud | Block | Skipped | N/A | No | Not executed: destructive action |
| Fraud | Fraud | Transfer to support | UI-only / no backend action observed | N/A | No | Recorded as note |
| Admin | Admin | User list / roles view | OK | 200 | No | Targeted UI rerun passed |
| Admin | Admin | Technical details | OK | 200 | No | Checked on QA transfer |
| Admin | Admin | Retry processing | OK | 202 | No | Checked on QA transfer; audit event created |
| Admin | Admin | Role toggles | Visual only | N/A | No | Not executed: destructive action on seed users |
| Admin | Admin | Block/unblock user | Skipped | N/A | No | Not executed: destructive action on seed users |
| Auditor | Audit | Ledger search | OK | N/A | No | UI usable |
| Auditor | Audit | Audit search | OK | N/A | No | UI usable |
| Auditor | Audit | Open event/details | OK | N/A | No | Transfer event opening available where operation is a transfer |
| Auditor | Audit | Export CSV | Available | N/A | No | Not saved to repo |
| All | Topbar | Logout | OK | 204 | No | Protected route shows login after logout |

## 6. Успешные сценарии

- Backend healthcheck returned `ready/postgresql`.
- CORS preflight from Vercel origin to Railway `auth/login` passed.
- QA-001 is fixed: frontend production calls Railway backend directly.
- All 6 roles can login, call `auth/me`, call `auth/sessions`, refresh, and logout.
- Role-based cabinet access matches the expected matrix.
- Customer frontend flow created and confirmed QA transfer `c2ab2878-b022-4cf0-a66d-e3d1bf44590c`; final status observed as `Settled`.
- SupportOperator updated support claim on the QA transfer: status `Open`.
- AmlOfficer recorded `Allow` on the QA transfer: 202.
- FraudOfficer recorded `ManualReview` on the QA transfer: 202.
- Admin loaded technical details and requested retry on the QA transfer: 200/202.
- Auditor saw 5 audit events for the QA transfer after test actions.
- Responsive smoke at 1440, 768, and 390 px passed: nav/client content/logout visible, no 5xx API responses, no console errors.

## 7. Найденные ошибки

| ID | Роль | Страница | Действие/кнопка | Ожидалось | Получилось | HTTP status | Console error | Severity | Как воспроизвести | Предложение по исправлению | Статус |
|---|---|---|---|---|---|---|---|---|---|---|---|
| QA-001 | Any | Frontend API | Production data source | Frontend calls Railway backend | Calls Railway backend; no Vercel `/api/v1/auth/*` observed | 200/204 | No | High | Login via frontend and inspect network | Keep API base URL pinned to Railway in production env and cover with e2e test | Fixed |
| QA-002 | Any | Backend `/api/v1/auth/logout` | Clear documented logout contract | Logout without `Authorization` returns 401; with frontend Authorization returns 204 | 401 without auth, 204 with auth | No | Low | POST logout with only refresh token | Document Authorization requirement or allow refresh-token-only revocation | Open / Note |
| QA-003 | Any | Backend auth | Old access token after logout | If session revocation is expected, old access token should fail | Old access token still works until expiry | 200 | No | Medium | Login, logout with Authorization, call `auth/me` with old access token | Bind access tokens to sessions or document short-lived JWT behavior | Open / Note |
| QA-004 | User-facing UI/source | Frontend source | Russian text literals | Source/build text should be clean UTF-8 | `App.vue`, `savranpayApi.ts`, e2e docs/tests and production UI text checked as clean UTF-8; no mojibake tokens found | N/A | No | Medium | Inspect frontend source and run build/smoke checks | Keep source files UTF-8 and avoid terminal recoding during edits | Fixed |
| QA-005 | Frontend API | Vercel `/api/v1/demo`, `/api/v1/cabinets` | If frontend no longer proxies API, these routes are irrelevant | Frontend no longer depends on Vercel API routes | N/A | No | Low | GET old Vercel demo routes | Remove from production expectations; keep backend routes tested directly if needed | Obsolete |

## 8. Ошибки API/Network

- No 500/503 observed in main scenarios.
- Expected 403 responses in API role matrix are OK.
- Expected 401 after logout/unauthenticated access is OK when UI returns to login.
- One Playwright logout request recorded `net::ERR_ABORTED` after a 204 response; UI logout still completed and protected route required login. Treated as non-critical browser-side abort.
- Some direct matrix attempts hit transient connect/timeouts against Railway; targeted retries and UI/API scenario checks succeeded.

API policy sample:

| Роль | Expected OK endpoints | Expected forbidden endpoints |
|---|---|---|
| Customer | dashboard, accounts, transfers, support claims | risk, audit, ledger, admin users |
| SupportOperator | dashboard, accounts, transfers, support claims | risk, audit, ledger, admin users |
| AmlOfficer | dashboard, support claims, risk checks | accounts, transfers, audit, ledger, admin users |
| FraudOfficer | dashboard, support claims, risk checks | accounts, transfers, audit, ledger, admin users |
| Admin | dashboard, accounts, transfers, support, risk, audit, ledger, admin users | None in checked set |
| Auditor | dashboard, accounts, transfers, support, audit, ledger | risk, admin users |

## 9. Ошибки Console

No critical JavaScript console errors were captured during:

- production API binding check;
- role route checks;
- Customer create/confirm transfer flow;
- Support/Admin targeted reruns;
- responsive smoke checks.

## 10. Что не удалось проверить и почему

- Password change was not executed because it would alter shared seed credentials.
- Admin role remove/add and user block/unblock were not executed because visible users are seed/test users, not disposable QA users.
- AML/Fraud `Block` was not executed because it is destructive and not required after safe QA transfer decisions passed.
- Worker online status was not verified because no Railway logs/worker health endpoint were provided.
- Full HAR files were not saved by design, to avoid persisting tokens or sensitive headers.
- Pagination was not deeply exercised where the UI did not expose an obvious page control in the current data volume.

## 11. Рекомендации по исправлению

1. Keep the new Railway API binding covered by Playwright in CI.
2. Document logout behavior: refresh token is revoked, existing JWT can live until expiry.
3. Add disposable QA users/entities for destructive admin, role, block, reject, reset, revoke checks.
4. Fix mojibake in source strings before future UI changes accidentally expose broken text.
5. Add a worker health/log check endpoint or operational runbook step if worker status is required for production smoke.

## 12. Автоматические e2e-тесты

Updated:

- `frontend/savranpay-web/tests/e2e/production-smoke.spec.ts`
- `frontend/savranpay-web/tests/e2e/README.md`

Current tests:

1. Backend healthcheck.
2. Frontend production API binding to Railway backend, including no Vercel `/api/v1/*` auth calls.
3. Login/me/refresh/logout for all roles.
4. Role route access matrix.
5. Customer create transfer flow.
6. Optional customer confirmation flow when `SAVRANPAY_DEMO_TRANSFER_SECRET` is provided.

Verified locally:

- `npx playwright test tests/e2e/production-smoke.spec.ts --list` -> 6 tests detected.
- `SAVRANPAY_CUSTOMER_PASSWORD=... npx playwright test tests/e2e/production-smoke.spec.ts -g "frontend sends production API"` -> passed.

Secrets and credentials are read from env and are not stored in the repository.

## UI/UX polish verification

- Layout polish: refreshed light banking theme, sidebar/topbar spacing, panels, buttons, badges, table overflow, technical `<pre>` blocks, mobile navigation and login page copy.
- Pages covered by the polish: login, Customer, SupportOperator, AML, Fraud, Admin and Audit cabinets through shared layout, table, action, badge and detail styles.
- Screen sizes checked locally: 1440 px, 768 px and 390 px. Login page has no page-level horizontal scroll after the mobile layout fix. Customer cabinet login could not be completed locally because the ignored local `.env` points dev mode to `https://localhost:5001`; production binding was checked against Vercel/Railway instead.
- QA-004 mojibake: Fixed. Source files were verified as UTF-8 via Node checks; no common mojibake marker patterns were found in `frontend/savranpay-web/src`, e2e files or this report.
- Build: `npm run build` passed after the UI changes.
- Smoke/e2e: `npx playwright test tests/e2e/production-smoke.spec.ts --list` detected 6 tests. Full run without role passwords passed healthcheck and skipped 5 env-protected tests as designed. Targeted production API binding with `SAVRANPAY_CUSTOMER_PASSWORD` from env passed.
- Railway API binding: preserved. `savranpayApi.ts` now defaults to `https://savranpay-production.up.railway.app` when no explicit `VITE_API_BASE_URL` is provided, and targeted browser smoke confirmed auth/dashboard/sessions/logout requests go to Railway, not `https://savranpay-5yge.vercel.app/api/v1/*`.

## 13. Итоговый вывод

- Production frontend: работает.
- Production backend: работает.
- PostgreSQL: подключен, `/health/ready` returns `storage=postgresql`.
- Worker: не проверен, requires logs/worker health.
- Frontend API binding на Railway: подтвержден.
- Critical/High open issues: none.
- Итоговый статус: **PASS WITH NOTES**.
