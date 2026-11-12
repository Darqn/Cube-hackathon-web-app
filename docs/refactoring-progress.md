# Архитектурный рефакторинг: результаты

Рабочая ветка: `refactor/modular-architecture`. План согласован заказчиком после [аудита](architecture-audit.md). Исходный commit: `fb35da0d6b87078c3b3587742d51dc0d23f19ee6`.

## 1. Characterization перед изменением приложения

- Обнаружено: runtime OpenAPI не полностью описывает тела ответов; валидация зависит от namespace; документация не отражает фактический статус ошибки tenant-контекста при EF query evaluation.
- Решение: сохранить исходный OpenAPI без deployment-specific `servers` и дополнить проверками реальных JSON/ProblemDetails, null/length/collection boundaries, Location/security headers, audit/outbox и DI/EF model parity.
- Изменено: `ContractTests.cs`, `Baselines/openapi.json`, копирование baseline в test output. Production-код при запуске этих тестов ещё исходный.
- Преимущество: переносы кода теперь контролируются проверками поведения, а не только компиляцией.
- Проверено: `dotnet test Cube.sln --configuration Release --no-restore` с TRX, реальный изолированный PostgreSQL 17.7: **61 passed, 0 failed, 0 skipped**. Release build без предупреждений. Исходные 49 тестов сохранены.
- Риски: `GET /api/v1/documents` для Staff без tenant сейчас возвращает **500**, поскольку EF оборачивает `ApiError` при извлечении query parameters. Это поведение зафиксировано и не исправляется незаметно в рефакторинге; перевод в 403 требует отдельного согласования. Frontend и production deployment этим этапом не проверялись.

Результаты команд сохраняются вне исходников в `/workspace/.cube-tools/refactor-results`.

## 2. HTTP, validation и текущий пользователь

- Обнаружено: scope DbContext может возникнуть до установки authenticated principal; кеширование пользователя в конструкторе привело бы к потере audit.
- Решение: scoped `ICurrentActor` с HTTP-адаптером, читающим актуальные claims; явный marker `IApiRequest` для входных контрактов вместо namespace/`<Clone>$`; выделенный middleware и модульная регистрация Identity/Infrastructure.
- Изменено: Program теперь только собирает host и маршруты; CLI maintenance, middleware, validation, auth handler/tokens и contracts разнесены. CubeDb больше не принимает IHttpContextAccessor. Метаданные validation кешируются по типу, без изменения ограничений и кодов ошибок.
- Преимущество: persistence и сценариям доступен текущий пользователь без HTTP-зависимости; DTO можно переносить между namespaces без потери validation.
- Проверено: полный Release suite — **67 passed, 0 failed, 0 skipped**, без compiler warnings; OpenAPI и migration snapshot совпадают. Отдельные тесты проверяют actor до/после authentication, audit без HTTP и validation контракта вне Cube.Api.
- Риски: прежняя классификация DbUpdateException и поведение Response.HasStarted намеренно сохранены. Интерфейс текущего пользователя не заменяет проверки доступа к конкретным ресурсам.
