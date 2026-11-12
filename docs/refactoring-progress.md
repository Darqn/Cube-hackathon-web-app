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

## 5. Persistence, оформление и финальная проверка

- Обнаружено: общая EF-конфигурация скрывала владельцев ограничений/relationships; компактные многооператорные методы затрудняли чтение перемещённого кода.
- Решение: IEntityTypeConfiguration по владельцам с явной регистрацией; исходные conventions, CLR identity, constraints и индексы сохранены. Существующий editorconfig дополнен правилами оформления без подавления diagnostics; auto-properties остаются компактными.
- Изменено: конфигурации 22 сущностей вынесены из CubeDb; форматирование рукописного C# и удаление unused imports; добавлена текущая архитектура и ссылка README. Миграции исключены из форматирования.
- Преимущество: persistence-настройки находятся рядом с областью, а CubeDb показывает общие conventions и список конфигураций. Изменения оформления выделены отдельным коммитом.
- Проверено: locked restore, финальная Release build — **0 warnings, 0 errors**; полный suite — **97 passed, 0 failed, 0 skipped**. Все исходные 49 test cases сохранены, добавлено 48. Architecture, DI, JSON/OpenAPI и EF snapshot tests включены в существующую CI-команду. Fingerprints всех 12 исходных migration/SDK/lockfile artifacts совпадают; diff миграций, SDK pin, dependency lockfiles, appsettings и compose пуст.
- Проверено для frontend: `npm ci --no-audit --no-fund` по прежнему lockfile, `npm run typecheck` — успешно, `npm test` — **20 passed**. Зависимости не обновлялись; npm сообщает deprecation notices существующих transitive packages.
- Проверено в браузере: `expo export --platform web`, затем все существующие Playwright suites в локальном Chromium с реальными API/PostgreSQL — **13 passed**: admin 3, design 5, employee 3, portal 2. Проверены desktop/mobile workflows, приватные ответы/файлы, status/review и tenant portal. API перезапускался между suites, сохраняя исходные rate-limit defaults. Были выполнены CLI migrations и bootstrap для отдельной временной базы.
- Очистка: fixtures/browser-базы удалены; отдельный запрос к pg_database не обнаружил оставшихся `cube_tests_%`/`cube_refactor_browser_%`. Browser API/web процессы остановлены; временный PostgreSQL-контейнер удалён. Production данные и инфраструктура не использовались.
- Риски: browser run проверяет локальное Development окружение, а не production deployment/физические устройства. Реальные integrations, scanner, нагрузка и аварийное восстановление не проверялись.

Команды финальной проверки:

```bash
dotnet restore Cube.sln --locked-mode
dotnet build Cube.sln --configuration Release --no-restore
dotnet test Cube.sln --configuration Release --no-build --no-restore \
  --logger 'trx;LogFileName=final.trx' \
  --results-directory /workspace/.cube-tools/refactor-results
# tenant-app-frontend:
npm ci --no-audit --no-fund
npm run typecheck
npm test
```

Browser runner для данной сессии находится вне репозитория: `/workspace/.cube-tools/browser-check.py`. Он использовал существующие test suites без изменения тестовых сценариев, отдельные credentials/DB, Expo export и последовательный `npx playwright test <suite> --output <artifact-dir> --reporter=line`. Результаты: `/workspace/.cube-tools/browser-results/summary.json`, backend TRX — `/workspace/.cube-tools/refactor-results/final.trx`.

## Критерии завершения и оставшиеся ограничения

| Критерий | Результат |
| --- | --- |
| Организация по функциям | Выполнена для восьми областей; cross-feature workflow и API dashboards выделены отдельно |
| Сохранение API/БД/версий | Проверены OpenAPI 94 paths / 110 operations / 53 schemas, реальные JSON/error/security cases и browser workflows; EF snapshot и неизменность migration/lock artifacts |
| Сборка и прежние тесты | Release без warnings/errors; все прежние тесты проходят; 97 backend, 20 frontend, 13 browser scenarios |
| Существенное дублирование | Общая загрузка/rollback файлов, async notification I/O, resource visibility и request factory выделены |
| Бизнес-сценарии вне HTTP | Выполнено для Requests/Metering/RentalInquiry/Scheduler; часть простых CRUD и Identity операций сохраняет прямую EF/UserManager orchestration в endpoint. Полная миграция каждого endpoint в отдельный handler не выполнялась |
| Независимость от инфраструктуры | Domain чистый; application handlers используют EF непосредственно по принятому упрощению. Строгая Clean Architecture с независимой application assembly не достигалась и не заявляется |
| Автоматические границы | Проверяются domain/application dependencies и циклы namespaced module components; все обращения к чужим DbSet внутри общей сборки автоматически не изолированы |
| Производительность | Блокирующий notification DB I/O заменён async; количественное улучшение throughput не измерялось. Scheduler batching остаётся отдельной задачей |

Отдельного согласования требуют исправления исходного 500 без tenant-контекста, классификации DbUpdateException/Response.HasStarted, изменения бизнес-правил, API/OpenAPI, схемы БД, ключевых версий, production configuration и включения внешних integrations. Эти изменения не выполнялись. Файловая компенсация остаётся best-effort при process crash/cleanup failure; distributed transaction или recovery worker не добавлялись.
