# TZ_SavranPay_v1_1

## 1. Назначение

SavranPay предназначен для безопасного создания, проверки, подтверждения, исполнения и учета переводов денежных средств. Версия `v1.1` расширяет MVP до стенда, который можно запускать с PostgreSQL, JWT-авторизацией, ролевыми кабинетами, outbox/inbox и Docker Compose.

## 2. Роли

Система поддерживает роли:

- `Customer` - клиентский кабинет `/cabinet/client`.
- `SupportOperator` - кабинет поддержки `/cabinet/support`.
- `AmlOfficer` - AML-кабинет `/cabinet/aml`.
- `FraudOfficer` - антифрод-кабинет `/cabinet/fraud`.
- `Admin` - административный кабинет `/cabinet/admin`.
- `Auditor` - кабинет аудита `/cabinet/audit`.

## 3. Авторизация

Реализованы:

- вход по `login/password`;
- JWT access token;
- refresh token с хранением хеша в таблице `refresh_tokens`;
- привязка пользователя к ролям через `users`, `roles`, `user_roles`;
- конфигурационный флаг `Jwt__RequireAuthorization=true` для включения обязательной авторизации в production-контуре.

Демо-пользователи создаются при инициализации PostgreSQL:

- `client@savranpay.local / Client123!`
- `support@savranpay.local / Support123!`
- `aml@savranpay.local / Aml123!`
- `fraud@savranpay.local / Fraud123!`
- `admin@savranpay.local / Admin123!`
- `audit@savranpay.local / Audit123!`

## 4. PostgreSQL и EF Core

Добавлен `SavranPayDbContext`, миграция начальной схемы и таблицы:

- `users`
- `roles`
- `user_roles`
- `refresh_tokens`
- `accounts`
- `transfers`
- `ledger`
- `audit_events`
- `outbox_messages`
- `inbox_messages`
- `notifications`

Если строка подключения `ConnectionStrings__Postgres` не задана, приложение сохраняет учебный in-memory режим для локальных тестов.

## 5. Docker

Добавлены:

- `docker-compose.yml`;
- контейнер `backend`;
- контейнер `worker`;
- контейнер `frontend`;
- контейнер `postgres`;
- `.env.example`.

Локальный запуск:

```powershell
copy .env.example .env
docker compose up --build
```

## 6. Криптография

В учебном стенде подтверждение операции остается на HMAC-SHA-256/WebCrypto для воспроизводимости в браузере. Для production-контура добавлены конфигурационные параметры `ProductionCrypto` под интеграцию с HSM/KMS, WebAuthn/passkeys и сертифицированной СКЗИ/ГОСТ-реализацией через существующий контракт `ICryptoService`.

Полная промышленная криптография требует внешнего поставщика HSM/KMS/СКЗИ и не должна имитироваться демо-ключом в коде.

## 7. Outbox/inbox и уведомления

Реализованы таблицы `outbox_messages` и `inbox_messages`. Backend кладет события аудита и уведомлений в outbox, `BankTransfers.Workers` забирает недоставленные сообщения и передает уведомления в адаптер `INotificationSender`.

Текущий адаптер `LoggingNotificationSender` является заменяемым портом для email/SMS/push провайдера.

## 8. Мониторинг

Реализованы:

- `GET /health/live`;
- `GET /health/ready`;
- `GET /metrics` в Prometheus text format;
- structured JSON logging;
- Docker healthcheck для backend и PostgreSQL.

Для production-алертов требуется подключение внешней системы мониторинга к `/health/ready` и `/metrics`: Prometheus/Grafana, OpenTelemetry Collector, Sentry или аналог.

## 9. Frontend и публикация

Frontend поддерживает сборку `npm run build` и отдельные кабинеты. Для Netlify нужно указать переменную:

```text
VITE_API_BASE_URL=https://<public-backend-url>
```

Ссылка `https://savranpay.netlify.app` будет рабочей только при опубликованном публичном backend URL.

В репозитории добавлены `netlify.toml`, `render.yaml` и инструкция `docs/operations/public-domain-deployment.md`, чтобы после деплоя связать frontend с публичным backend-доменом.
