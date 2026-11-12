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

## 3. Заявки, показания и rental inquiry

- Обнаружено: endpoint-классы взаимно использовали query/review/factory helpers, multipart-ответ совмещал parsing и сохранение; Events выполнял синхронный DB I/O.
- Решение: отдельные обработчики создания, статуса, назначения, сообщения, attachment, multipart reply, submission/review показаний; независимые resource visibility queries; чистая фабрика/правила и `Outcome<T>` для ожидаемых ошибок. HTTP mapping остаётся в API. RentalInquiry — отдельный application workflow.
- Изменено: модульные DI регистрации Requests/Metering; actor/tenant/lease scopes; async notifications с CancellationToken; resource selection до multipart parsing; FileWriteBatch и файловый порт с rollback, включая partial writes/cancellation. Правила INN и счетов получили собственных владельцев. C# request DTO перенесены с сохранением имён схем и wire fields.
- Преимущество: сценарии вызываются без HttpContext/IResult; исключён цикл RequestEndpoints/StaffEndpoints, общий factory не принадлежит HTTP. Транзакции, optimistic concurrency, CAS review, notifications/outbox/audit сохраняются.
- Проверено: **93 passed, 0 failed, 0 skipped**, Release без warnings/errors; OpenAPI 94/110 и EF snapshot совпадают. Добавлены pure rule cases, прямой вызов application handler с реальным PostgreSQL без HTTP, проверки отказа без side effects, UTC lease boundaries, файловые failure/cancellation tests. Architecture tests анализируют signatures и IL, включая async state machines.
- Риски: разрешён прямой EF в feature handlers; одна сборка даёт логические границы, а не строгую compile-time изоляцию. EF entity namespaces пока сохранены ради metadata. Infrastructure/Identity EF-зависимости не считаются HTTP-типами в application boundary test. Остальные модули требуют последовательной обработки.

## 4. Остальные области и общий storage

- Обнаружено: роли admin/tenant и общий Details распределяли одну функцию между разными файлами, scheduler вызывал NewsEndpoints, upload logic существовала в трёх вариантах.
- Решение: маршруты и модели получили владельцев Identity, Property, Billing, Documents, News, Notifications; dashboards остались API-композицией. DTO-контейнеры сохраняют прежние wire names. NewsAudience — независимый запрос, NotificationScheduler — scoped сценарий, worker — только lifecycle/scope.
- Изменено: перенос моделей без смены CLR namespace, разделение административных и detail маршрутов, общий UploadedFileReader/FileWriteBatch для всех трёх upload paths; использование TimeProvider в оставшихся date-based HTTP queries. Staff inbox получает resource visibility из application-компонентов. Обновлена ссылка README на integration contracts.
- Преимущество: для добавления возможности понятен модуль-владелец; scheduler не зависит от HTTP; file validation/quarantine/write/rollback сопровождаются в одном месте.
- Проверено: **97 passed, 0 failed, 0 skipped**, Release без warnings/errors. Новые integration cases проверяют фактическое удаление файла после реального отказа PostgreSQL, targeting news attachments, invalid listing photo и отзыв фото после archive, preferences/blocking/deduplication scheduler. OpenAPI и EF snapshot совпадают.
- Риски: scheduler пока сохраняет запросы на каждого пользователя и отдельные INSERT — batching требует измерений. Простые CRUD и отдельные Identity/administration сценарии пока используют прежние прямые вызовы EF/UserManager и Guard; полный переход всего API на CQRS/Outcome не выполняется ради паттерна.
