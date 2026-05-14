# SavranPay

SavranPay - демонстрационный банковский сервис перевода денежных средств на C#/.NET и Vue.

Проект показывает, как устроить безопасный процесс перевода: распоряжение клиента, проверка счета и лимитов, AML/KYC, антифрод, криптографическое подтверждение, ledger-учет, аудит, уведомления и обработка спорных операций.

## Структура

```text
src/
  BankTransfers.Api              HTTPS API и встроенный демо-кабинет
  BankTransfers.Application      сценарии использования, команды, DTO
  BankTransfers.Domain           бизнес-правила и доменные сущности
  BankTransfers.Infrastructure   demo-хранилище, AML, антифрод, криптография, аудит
  BankTransfers.Workers          фоновые обработчики
  BankTransfers.SharedKernel     общие базовые типы

tests/
  BankTransfers.UnitTests
  BankTransfers.IntegrationTests
  BankTransfers.SecurityTests
  BankTransfers.ContractTests

docs/
  architecture
  api
  compliance
  threat-model
  operations
```

## Локальный запуск

```powershell
dotnet build
dotnet test --no-build
dotnet run --project src\BankTransfers.Api\BankTransfers.Api.csproj --launch-profile https
```

Открыть кабинет:

```text
https://localhost:5001
```

Основные API:

```text
https://localhost:5001/api/v1/dashboard
https://localhost:5001/api/v1/accounts
https://localhost:5001/api/v1/transfers
https://localhost:5001/api/v1/ledger
https://localhost:5001/api/v1/audit-events
```

## Ограничения MVP

Текущая версия является учебным MVP. В ней используется in-memory-хранилище и демонстрационная HMAC-SHA-256 модель подтверждения операции. Для production нужны PostgreSQL/MS SQL Server, миграции, OAuth/OIDC, HSM/KMS или сертифицированное СКЗИ, полноценная модель ролей, outbox/inbox и отдельный Vue 3 + TypeScript + Vite frontend.
