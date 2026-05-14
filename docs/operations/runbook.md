# Runbook SavranPay

## Локальный запуск

```powershell
dotnet build
dotnet test --no-build
dotnet run --project src\SavranPay.Api\SavranPay.Api.csproj --launch-profile https
```

## Проверка доступности

```text
https://localhost:5001
https://localhost:5001/api/v1/dashboard
```

## Что проверить вручную

1. Открывается кабинет SavranPay.
2. Загружаются счета.
3. Создается перевод.
4. Перевод получает статус `PendingClientConfirmation`.
5. Подтверждение создает криптографический challenge.
6. После подтверждения появляется статус `Settled`.
7. Создаются ledger-записи.
8. Создаются AML/Fraud-записи.
9. Создаются audit events.
10. Спорная операция переводит статус в `Disputed`.

## Известные ограничения

Текущий стенд использует in-memory-хранилище. После перезапуска приложения данные сбрасываются.
