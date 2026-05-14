# SavranPay

SavranPay - учебный банковский сервис переводов денежных средств на C#/.NET и Vue 3. Проект показывает полный контур обработки перевода: авторизация пользователя, ролевые кабинеты, создание распоряжения, проверки AML/антифрод, подтверждение операции, ledger-учет, аудит, уведомления и подготовка к публикации frontend на публичном домене.

## Что реализовано

- Backend API на ASP.NET Core 8.
- Frontend на Vue 3 + TypeScript + Vite.
- Авторизация по `login/password`.
- JWT access token и refresh token.
- Backend logout, current-user endpoint, password change and session list.
- Admin API для пользователей, ролей, блокировки и разблокировки.
- Manual decision API для AML/Fraud кабинетов.
- Роли: `Customer`, `SupportOperator`, `AmlOfficer`, `FraudOfficer`, `Admin`, `Auditor`.
- Отдельные кабинеты:
  - `/cabinet/client`
  - `/cabinet/support`
  - `/cabinet/aml`
  - `/cabinet/fraud`
  - `/cabinet/admin`
  - `/cabinet/audit`
- PostgreSQL + EF Core.
- Миграция начальной схемы БД.
- Таблицы `users`, `roles`, `user_roles`, `refresh_tokens`, `user_sessions`, `accounts`, `transfers`, `risk_checks`, `ledger`, `audit_events`, `outbox_messages`, `inbox_messages`, `notifications`.
- Outbox worker для обработки событий и уведомлений.
- Health checks: `/health/live`, `/health/ready`.
- Метрики в Prometheus text format: `/metrics`.
- Structured JSON logging.
- Docker Compose для локального production-like запуска.
- Конфиги публикации: `render.yaml` для backend/worker/PostgreSQL и `netlify.toml` для frontend.

## Структура проекта

```text
src/
  SavranPay.Api              ASP.NET Core API, JWT, endpoints, health, metrics
  SavranPay.Application      use cases, handlers, interfaces
  SavranPay.Domain           domain model: accounts, transfers, money, risk
  SavranPay.Infrastructure   EF Core, PostgreSQL, auth, audit, crypto, notifications
  SavranPay.Workers          outbox worker
  SavranPay.SharedKernel     common domain primitives

frontend/
  savranpay-web                  Vue 3 + TypeScript frontend

tests/
  SavranPay.UnitTests
  SavranPay.IntegrationTests
  SavranPay.SecurityTests
  SavranPay.ContractTests

docs/
  api
  architecture
  compliance
  operations
  threat-model
  TZ_SavranPay_v1_1.md
```

## Быстрый запуск без PostgreSQL

Этот режим нужен для быстрых тестов и разработки. Если строка подключения PostgreSQL не задана, backend использует in-memory demo-store.

```powershell
dotnet build
dotnet test --no-build
dotnet run --project src\SavranPay.Api\SavranPay.Api.csproj --launch-profile https
```

Backend:

```text
https://localhost:5001
```

Frontend:

```powershell
cd frontend\savranpay-web
npm install
npm run dev
```

Открыть:

```text
http://localhost:5173/cabinet/client
```

## Запуск через Docker Compose

Docker Compose поднимает PostgreSQL, backend, worker и frontend.

```powershell
copy .env.example .env
docker compose up --build
```

После запуска:

```text
Frontend: http://localhost:5173/cabinet/client
Backend:  http://localhost:8080
Health:   http://localhost:8080/health/ready
Metrics:  http://localhost:8080/metrics
```

## Демо-пользователи

При первом запуске с PostgreSQL создаются пользователи:

```text
client@savranpay.local  / Client123!
support@savranpay.local / Support123!
aml@savranpay.local     / Aml123!
fraud@savranpay.local   / Fraud123!
admin@savranpay.local   / Admin123!
audit@savranpay.local   / Audit123!
```

## Основные API

```text
POST /api/v1/auth/login
POST /api/v1/auth/refresh
POST /api/v1/auth/logout
GET  /api/v1/auth/me
POST /api/v1/auth/change-password
GET  /api/v1/auth/sessions

GET  /api/v1/dashboard
GET  /api/v1/accounts
GET  /api/v1/transfers
POST /api/v1/transfers
GET  /api/v1/transfers/{transferId}
GET  /api/v1/transfers/{transferId}/confirmation-challenge
POST /api/v1/transfers/{transferId}/confirm
POST /api/v1/transfers/{transferId}/unauthorized-claim

GET  /api/v1/ledger
GET  /api/v1/audit-events
GET  /api/v1/risk-checks
GET  /api/v1/cabinets

GET  /health/live
GET  /health/ready
GET  /metrics

GET    /api/v1/admin/users
POST   /api/v1/admin/users
PATCH  /api/v1/admin/users/{userId}
POST   /api/v1/admin/users/{userId}/roles
DELETE /api/v1/admin/users/{userId}/roles/{role}
POST   /api/v1/admin/users/{userId}/block
POST   /api/v1/admin/users/{userId}/unblock

POST   /api/v1/aml/transfers/{transferId}/decision
POST   /api/v1/fraud/transfers/{transferId}/decision
```

OpenAPI-спецификация находится в `docs/api/openapi.yaml`.

## PostgreSQL и миграции

EF Core контекст: `SavranPayDbContext`.

Миграция начальной схемы лежит в:

```text
src/SavranPay.Infrastructure/Migrations
```

Backend автоматически применяет миграции при старте, если задана строка подключения:

```text
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=savranpay;Username=savranpay;Password=change-me
```

## Авторизация и роли

В production-like режиме включайте:

```text
Jwt__RequireAuthorization=true
Jwt__SigningKey=<strong-secret-key>
```

При включенной авторизации API проверяет Bearer JWT и роли пользователя. Frontend сохраняет access token и отправляет его в заголовке:

```text
Authorization: Bearer <token>
```

## Outbox, inbox и уведомления

Backend сохраняет события в `outbox_messages`. Worker `SavranPay.Workers` забирает недоставленные события и передает уведомления через `INotificationSender`.

AML/Fraud decisions сохраняются в `risk_checks`, поэтому кабинеты AML/Fraud работают и в PostgreSQL-режиме, а не только в in-memory demo.

Сейчас используется `LoggingNotificationSender`. Для production его нужно заменить на email/SMS/push адаптер.

## Криптография

В учебном стенде подтверждение перевода использует HMAC-SHA-256 и WebCrypto, чтобы сценарий можно было воспроизвести локально.

Для production предусмотрена точка расширения `ICryptoService` и настройки `ProductionCrypto`. Реальная промышленная реализация должна подключать HSM/KMS, WebAuthn/passkeys или сертифицированную СКЗИ/ГОСТ-библиотеку. Демо-HMAC не является production-криптографией.

## Публичный деплой

Подготовлены конфиги:

```text
render.yaml      backend + worker + PostgreSQL
netlify.toml     frontend
```

Порядок публикации:

1. Опубликовать репозиторий в GitHub.
2. Создать Render Blueprint из `render.yaml`.
3. Получить публичный URL продукта `https://savranpay.onrender.com`.
4. Открыть `/health/ready` и убедиться, что PostgreSQL готов.
5. Открыть `/cabinet/client` и войти демо-пользователем.

Vue frontend также собирается внутрь backend Docker-образа, поэтому для демонстрации достаточно одного публичного URL. Netlify остаётся опциональным вариантом отдельного frontend-деплоя.

Подробная инструкция: `docs/operations/public-domain-deployment.md`.

## Документация

- ТЗ: `docs/TZ_SavranPay_v1_1.md`
- Краткая ссылка на ТЗ: `TZ_SavranPay_v1_1.md`
- Архитектура: `docs/architecture`
- OpenAPI: `docs/api/openapi.yaml`
- Compliance: `docs/compliance`
- Threat model: `docs/threat-model/threat-model.md`
- Runbook: `docs/operations/runbook.md`

## Проверка проекта

```powershell
dotnet build
dotnet test --no-build
cd frontend\savranpay-web
npm run build
```

Последняя проверка проекта проходила успешно для backend, тестов и frontend-сборки.
