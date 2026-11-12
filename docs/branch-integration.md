# Объединение веток для проверки

Подготовлена ветка `integration/all-branches-review` от `origin/main` (`fb35da0`). Она предназначена для проверки до переноса одного итогового коммита в `main`. Основная ветка не меняется без результата пользовательской проверки. Исходные ветки сохраняются.

## Инвентаризация

Проверены все пять удалённых веток после `git fetch --all`:

| Ветка | Зафиксированный head | Что сохраняется |
| --- | --- | --- |
| `main` | `fb35da0` | Исходное приложение, кабинеты, демоданные и исправления запуска Windows |
| `dev/web-design` | `3e85390` | Дизайн, логотип Дальреммаш, адаптивные формы; уже входит в историю `main` |
| `refactor/modular-architecture` | `4069665` | Восемь модулей, вынесенные сценарии и правила, явная DI-композиция, EF configurations, архитектурные и контрактные тесты |
| `fix/expo-qr-launcher` | `e61aab3` | Включает весь рефакторинг; добавляет отдельную интерактивную консоль Expo QR, LAN/API/CORS, проверки Windows PowerShell и инструкции |
| `feature/meter-ocr` | `9110b58` | Новый авторизованный OCR endpoint, Tesseract, кнопка в клиенте, ручная проверка результата, образец и parser-тесты |

Локальные `main`, `refactor/modular-architecture` и `fix/expo-qr-launcher` совпадали с соответствующими удалёнными heads. Уникальных локальных изменений до начала объединения не было.

Общая история учтена: дизайн не накладывался повторно поверх `main`, рефакторинг получен вместе с QR-веткой. Независимый OCR применён отдельно. Результат подготовлен через squash, без переноса промежуточной истории в будущий коммит `main`.

## Решение пересечений

Единственный текстовый конфликт — `Program.cs`: OCR-ветка добавляла регистрацию/маршрут в прежнюю большую точку входа. Сохранена модульная композиция, `MeterOcr` регистрируется в `AddMeteringModule()`, маршрут подключается явно. Реализация находится в Metering: HTTP adapter, инфраструктурный запуск Tesseract и чистый разбор целого показания.

Два дефекта OCR исправлены при интеграции: процесс теперь завершается и при отмене HTTP-запроса; поздний ответ не перезаписывает изменённую пользователем форму. Десятичные разделители сохраняются движком и отклоняются parser, как предусматривает исходное ограничение на целые показания. Отправка показаний остаётся отдельным подтверждаемым действием. [Установка и проверка OCR](meter-ocr.md).

Исходный OpenAPI baseline не переписан. Проверяется отдельное добавление `POST /api/v1/meters/recognize`, затем все прежние пути/схемы/метаданные сравниваются с неизменённым эталоном. Миграции, EF snapshot, .NET SDK и lockfiles не меняются. Дизайн и QR launcher сохранены; `start-cube.bat` сохраняет CRLF.

## Проверки

Локальная проверка объединённого кода завершена:

- Locked restore и Release build: успешно, 0 warnings / 0 errors.
- Backend: **117 passed, 0 failed, 0 skipped** на PostgreSQL 17.7 / .NET SDK 10.0.403 / Tesseract 5.5.0. Включены API compatibility, EF snapshot parity, DI scope validation, архитектурные границы и реальные OCR HTTP/process тесты.
- TypeScript: `npm run typecheck` успешно; клиентские API/правила: **21 passed**.
- Expo web export успешно. Chromium/Playwright с реальным API и временными PostgreSQL/аккаунтами: **17 passed** — admin 3, design 5, employee 3, OCR 4, portal 2. После исправления авторизации в новом тесте чтения истории повторно прогнаны OCR и portal; успешно прошедшие остальные suites не менялись.
- PowerShell 7 на Linux: `scripts/test-windows-launcher.ps1` успешно; проверены syntax, ports, LAN/local, отдельная консоль без перенаправления вывода, CI reset и cleanup. Тест использует process mocks; полноценный запуск BAT на Windows этим не подменяется. Windows PowerShell 5 проверяется отдельным GitHub Actions job после push.
- SHA-256 всех 9 migration/snapshot файлов, `global.json` и NuGet lockfiles совпадают с исходными; frontend package/lockfile неизменны. Дизайн и launcher сохранены. Проверка whitespace diff успешна.

Команды:

```bash
dotnet restore Cube.sln --locked-mode
dotnet build Cube.sln --configuration Release --no-restore
dotnet test Cube.sln --configuration Release --no-build
cd tenant-app-frontend
npm run typecheck
npm test
```

Для backend-тестов нужны Tesseract и изолированная PostgreSQL с ролью `CREATEDB`; соединение передаётся через `CUBE_TEST_DATABASE`. Для браузерных тестов нужны собранный web-клиент, тестовый API и временные аккаунты; сценарии находятся в `tenant-app-frontend/tests/browser`. Проверки launcher: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-windows-launcher.ps1`.

## Проверка перед main

```bash
git fetch origin
git switch integration/all-branches-review
```

На Windows запустите `start-cube.bat`. Проверьте вход, кабинеты, основные операции, отдельное окно QR и подключение телефона в одной LAN. Для входа используйте свои локальные аккаунты либо [отдельную инструкцию демоданных](demo-data.md); в git пароли не добавляются. Для OCR установите движок и проверьте образец по инструкции выше. Совместимость Expo SDK 51 с установленным Expo Go/development build и правила Windows Firewall описаны в [инструкции QR](windows-expo-qr.md).

После пользовательской проверки итоговый commit можно перенести в `main` одним fast-forward, если `main` всё ещё указывает на исходный `fb35da0`. Если `main` изменится, сначала необходимо учесть новые изменения и повторить затронутые проверки. В этом этапе переключение/коммит/push в `main` не выполняются.

Ограничения: полноценный запуск Windows BAT с Docker Desktop и физические Android/iOS устройства в Linux-среде не проверяются. OCR не аттестован на реальных фотографиях разных счётчиков. Прежние ограничения внешних интеграций, production file scanning и обработки отдельных ошибок сохраняются; они перечислены в [архитектуре](architecture.md).
