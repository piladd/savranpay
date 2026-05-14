# Архитектура сервиса переводов

Проект построен как модульный монолит с разделением на слои Clean Architecture.

```mermaid
flowchart TB
    Client["Mobile/Web Client"] --> Api["SavranPay.Api"]
    Api --> Application["SavranPay.Application"]
    Application --> Domain["SavranPay.Domain"]
    Application --> Infrastructure["SavranPay.Infrastructure"]
    Infrastructure --> Database["PostgreSQL / MS SQL Server"]
    Infrastructure --> Broker["Message Broker"]
    Broker --> Workers["SavranPay.Workers"]
```

## Слои

`SavranPay.Api` принимает HTTP-запросы, проверяет заголовки и вызывает сценарии приложения.

`SavranPay.Application` содержит команды, обработчики, интерфейсы внешних зависимостей и бизнес-сценарии.

`SavranPay.Domain` содержит сущности, value objects, статусы и правила переходов.

`SavranPay.Infrastructure` содержит реализации репозиториев, AML, антифрода, криптографии и аудита.

`SavranPay.Workers` предназначен для фоновой обработки outbox, уведомлений и сверок.
