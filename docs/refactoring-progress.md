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
