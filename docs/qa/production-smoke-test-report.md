# SavranPay production smoke/e2e audit

Дата проверки: 2026-05-15  
Frontend: https://savranpay-5yge.vercel.app  
Backend API: https://savranpay-production.up.railway.app  
Итоговый статус: **PARTIAL PASS**

## Проверенные URL

| URL | Результат |
|---|---|
| https://savranpay-5yge.vercel.app | 200 OK |
| https://savranpay-5yge.vercel.app/cabinet/client | 200 OK, SPA route |
| https://savranpay-production.up.railway.app/health/ready | 200 OK, `{"status":"ready","storage":"postgresql"}` |
| https://savranpay-production.up.railway.app/swagger | 200 OK |
| https://savranpay-production.up.railway.app/swagger/v1/swagger.json | 404, Swagger JSON endpoint not exposed |
| https://savranpay-5yge.vercel.app/api/v1/auth/login | 200 OK, Vercel demo API |
| https://savranpay-5yge.vercel.app/api/v1/demo | 404, route absent in Vercel demo API |
| https://savranpay-5yge.vercel.app/api/v1/cabinets | 404, route absent in Vercel demo API |

Важное наблюдение: production frontend использует relative `/api/v1/*` и по `vercel.json` проксирует их в Vercel serverless demo-router. Он не обращается напрямую к Railway backend API.

## Проверенные учетные записи

Найдены в `README.md`, `docs/TZ_SavranPay_v1_1.md`, `src/SavranPay.Infrastructure/Persistence/SavranPayDbInitializer.cs`.

| Роль | Логин | Пароль | Backend login/me/refresh | Frontend login/logout |
|---|---|---|---|---|
| Customer | `client@savranpay.local` | `Client123!` | OK | OK |
| SupportOperator | `support@savranpay.local` | `Support123!` | OK | OK |
| AmlOfficer | `aml@savranpay.local` | `Aml123!` | OK | OK |
| FraudOfficer | `fraud@savranpay.local` | `Fraud123!` | OK | OK |
| Admin | `admin@savranpay.local` | `Admin123!` | OK | OK |
| Auditor | `audit@savranpay.local` | `Audit123!` | OK | OK |

`auth/logout` на backend возвращает 401 без `Authorization`, но успешно отрабатывает с `Authorization: Bearer ...`, как и делает frontend. После logout access token остается валидным до истечения срока, refresh token отзывается.

## Таблица ролей и доступных кабинетов

| Роль | `/client` | `/support` | `/aml` | `/fraud` | `/admin` | `/audit` |
|---|---:|---:|---:|---:|---:|---:|
| Customer | OK | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI |
| SupportOperator | Forbidden UI | OK | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI |
| AmlOfficer | Forbidden UI | Forbidden UI | OK | Forbidden UI | Forbidden UI | Forbidden UI |
| FraudOfficer | Forbidden UI | Forbidden UI | Forbidden UI | OK | Forbidden UI | Forbidden UI |
| Admin | OK | OK | OK | OK | OK | OK |
| Auditor | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI | Forbidden UI | OK |

Backend API role matrix also matches expected policy boundaries: admin sees all checked endpoints; customer cannot read admin/risk/audit/ledger; AML/Fraud can read risk checks but not admin/ledger/audit; auditor can read audit/ledger but not admin/risk.

## Таблица проверенных кнопок

| Роль | Страница | Кнопки/действия | Результат |
|---|---|---|---|
| Guest | login | `Войти`, sidebar cabinet buttons | Login works; protected routes show login/forbidden state |
| Customer | client | show/hide account number, refresh sessions, create transfer, refresh signature data, sign/confirm, cancel, open transfer, repeat, dispute, logout | Create and confirm test transfer OK in UI; number visibility OK; logout OK |
| Customer | client | change password | Not executed: destructive for shared seed account |
| SupportOperator | support | search, category select, open transfer, save support claim, create dispute | Support claim tested via backend on QA transfer; no real claims closed |
| AmlOfficer | aml | search, open review, Allow, ManualReview, Block, Request documents | Allow tested via backend on QA transfer; Block not executed as dangerous; Request documents is UI-only/disabled-noop style |
| FraudOfficer | fraud | search, open review, Allow, Step-up, Block, Transfer to support | ManualReview/Step-up tested via backend on QA transfer; Block not executed as dangerous; Transfer to support appears UI-only |
| Admin | admin | refresh users, role toggles, block/unblock user, open operation, retry processing, technical details | Technical details and retry tested on QA transfer; role toggles and block/unblock not executed on seed users |
| Auditor | audit | ledger search, audit search, export CSV, open event | Search/open checked visually; export CSV available when events exist |
| All authenticated | topbar | `Выйти` | OK; protected route after logout shows login form |

## Успешные сценарии

- Backend readiness returns expected PostgreSQL-ready payload.
- All six seeded role accounts can login, call `auth/me`, refresh tokens, and logout with Authorization header.
- Frontend login/logout works for all six roles.
- Customer UI created and confirmed one test transfer on the Vercel demo API.
- Backend API created and confirmed test transfer `0583ebd3-6d45-4ac4-a01f-1dac1a681a3d`; final status `Settled`.
- Backend support/AML/fraud/admin/audit flow used test transfer `6633a592-8153-4575-8a53-febdbcc0d743`: support claim `Open`, AML `Allow`, Fraud `ManualReview`, admin technical details OK, retry audit event OK, auditor saw 4 events for the transfer.
- Responsive smoke at 1440, 768, and 390 px: sidebar/nav and primary controls remained available; no console errors were captured.

## Найденные ошибки

| ID | Роль | Страница | Действие/кнопка | Ожидалось | Получилось | HTTP status | Console error | Severity | Как воспроизвести | Предложение по исправлению |
|---|---|---|---|---|---|---|---|---|---|---|
| QA-001 | Any | Frontend API | Production frontend data source | Frontend should use Railway backend API or documented production API | Frontend calls Vercel `/api/v1/*` demo-router; tokens have `demo.*` prefix and data differs from Railway PostgreSQL | 200 | No | High | Login at frontend, inspect `/api/v1/auth/me`; compare token/user id with Railway API | Set `VITE_API_BASE_URL=https://savranpay-production.up.railway.app` for production or document Vercel as intentional demo backend |
| QA-002 | Any | Backend `/api/v1/auth/logout` | Logout after refresh without Authorization | Logout endpoint should either require and document Authorization, or accept refresh-token-only logout | Request without Authorization returns 401; frontend path is OK because it sends Authorization | 401 | No | Low | POST `/api/v1/auth/logout` with only refresh token | Keep current behavior but document it, or allow refresh-token-only revocation |
| QA-003 | Any | Backend `/api/v1/auth/logout` | Use old access token after logout | After logout, protected endpoints should reject old session token if session revocation is required | Old access token still works until expiry | 200 | No | Medium | Login, logout with Authorization, then call `/api/v1/auth/me` with old access token | Bind access tokens to active sessions or shorten TTL and document logout semantics |
| QA-004 | User-facing UI | All pages | Read labels/text | Russian UI text should be clean in source/build | Production UI is mostly readable, but source contains mojibake strings; risk of broken text in future builds and API default messages | N/A | No | Medium | Open `frontend/savranpay-web/src/App.vue` and `savranpayApi.ts` | Re-save source files as UTF-8 and replace mojibake literals with correct Russian text |
| QA-005 | Frontend API | `/api/v1/demo`, `/api/v1/cabinets` | Demo/cabinets endpoints from backend contract | Either available through production frontend proxy or clearly excluded | Vercel API returns unknown route | 404 | No | Low | GET frontend `/api/v1/demo` or `/api/v1/cabinets` | Add routes to Vercel router or avoid exposing these URLs in frontend deployment expectations |

## Ошибки API/Network

- No 500/503 observed.
- Expected 403 responses were observed for forbidden role/API combinations.
- 404 observed for Swagger JSON and Vercel demo/cabinets routes; Swagger JSON absence is acceptable for production, Vercel route absence is low severity unless the frontend is expected to proxy all backend routes.

## Ошибки Console

No JavaScript console errors were captured during the role navigation, customer transfer, and responsive smoke checks.

## Что не удалось проверить и почему

- Password change was not executed because it would alter shared production seed credentials.
- Admin block/unblock user and role add/remove were not executed because visible users are real seeded accounts, not disposable QA users.
- AML/Fraud `Block` was not executed except marked as dangerous, because it could block a non-disposable operation.
- Screenshot files were not saved during the run because the current workspace was read-only while browser automation was running.
- Full Network waterfall from browser DevTools was approximated with direct API checks and console logs; the in-app browser API available here did not expose a full request log.

## Рекомендации по исправлению

1. Decide whether `savranpay-5yge.vercel.app` is a demo frontend or true production frontend. If true production, wire it to Railway API with `VITE_API_BASE_URL`.
2. Add a disposable QA admin-created user/transfer fixture for destructive smoke checks.
3. Document logout semantics or revoke access tokens by session.
4. Fix source encoding/mojibake before it leaks into validation/default messages.
5. Add Playwright smoke checks to CI with environment-provided credentials and secrets.

## Автоматические e2e-тесты

Добавлены Playwright smoke tests:

- `frontend/savranpay-web/tests/e2e/production-smoke.spec.ts`
- `frontend/savranpay-web/tests/e2e/README.md`

Проверено локально:

- `npm run build` - passed.
- `npx playwright test tests/e2e/production-smoke.spec.ts --list` - 5 tests detected.
- `npx playwright test tests/e2e/production-smoke.spec.ts -g "backend healthcheck"` - passed.

Полный запуск требует env-пароли для всех ролей и установленный браузер Playwright. Production secret для подтверждения перевода не зашит в код и читается только из `SAVRANPAY_DEMO_TRANSFER_SECRET`.
