# QR Expo при запуске Windows BAT

## Причина и выбранное решение

Старый `start-cube.bat` запускал Expo через `Start-Process -WindowStyle Hidden` с перенаправлением stdout/stderr в файлы. В установленном Expo CLI интерактивность определяется как `!CI && process.stdout.isTTY`; без TTY блок QR и горячие клавиши не выводятся.

Исправленный launcher оставляет API в фоне, а Expo запускает в отдельной видимой консоли **Cube - Expo QR** без перенаправления потоков. Унаследованный `CI` сбрасывается только внутри процесса Expo. Основное окно сохраняет управление всеми дочерними процессами и базой, запущенной этим launcher.

## Обычный запуск на телефоне

1. Подключите компьютер и телефон к одной доступной Wi-Fi/LAN сети. Гостевая Wi-Fi сеть с изоляцией устройств может блокировать соединение.
2. Запустите `start-cube.bat` либо `start-cube.bat "C:\Projects\Cube-hackathon-web-app"`.
3. После подготовки откроется окно **Cube - Expo QR**. Дождитесь строки `Metro waiting on exp://...:8081` и QR. При запуске в development build Expo может показывать другой совместимый deep link.
4. Сканируйте QR совместимым клиентом. Если код ушёл выше из-за сообщений, нажмите **c** в окне Expo — оно очистится и QR появится снова. **r** перезагружает приложение, **w** открывает веб-версию, **s** переключает Expo Go / development build.
5. Останавливайте весь проект клавишей **q + Enter в основном окне BAT**. Ctrl+C в Expo останавливает только Expo; затем всё равно завершите основной launcher через q.

Node.js и dotnet должны быть доступны для телефона через Windows Firewall в доверенной частной сети: Metro — TCP 8081, API — TCP 5080. Проверьте с телефона `http://<IP компьютера>:5080/health/ready`: должен вернуться `Ready`. QR открывает Metro; доступность API проверяется отдельно.

Проект закреплён на **Expo SDK 51**. Наличие QR не делает новый Expo Go совместимым со старым SDK: используйте поддерживающий SDK 51 клиент ([официальная страница Expo Go](https://expo.dev/go)) либо соответствующую development build. Для физического iPhone доступность старого Expo Go ограничена; development build может быть необходима. Версия SDK этим исправлением не обновляется.

## Несколько адаптеров, VPN или неверный IP

Автоматически выбирается первый частный IPv4 подключённого Ethernet/Wi-Fi адаптера с IPv4 gateway. При нескольких адресах launcher сообщает выбор. Если выбран не тот интерфейс, добавьте в корневой `.env`:

```dotenv
CUBE_EXPO_HOST='lan'
CUBE_LAN_IP='192.168.1.20'
```

Замените пример фактическим IPv4 вашего Wi-Fi/Ethernet из `ipconfig`. Override должен быть частным IPv4, назначенным активному адаптеру этого компьютера; публичные, loopback и неназначенные адреса отклоняются. У ручного выбора нет требования gateway — это позволяет использовать локальную сеть без выхода в Интернет.

Для этой сессии launcher согласует:

- `REACT_NATIVE_PACKAGER_HOSTNAME` — адрес в QR Metro;
- `EXPO_PUBLIC_API_URL` — `http://<тот же IP>:5080/api/v1`;
- API bind — `127.0.0.1:5080` и выбранный IP:5080;
- CORS — localhost/127.0.0.1 и выбранный LAN web origin на 8081.

Существующий `.env` не перезаписывается. Настройки вычисляются в окружении запуска, production configuration и настройки PostgreSQL не меняются. Metro запускается с `--clear`, чтобы после смены сети в bundle не оставался прежний API URL. Если подходящего адреса нет, launcher явно сообщает о fallback в localhost: веб продолжает работать, физический телефон подключиться не сможет.

Для запуска только на своём компьютере:

```dotenv
CUBE_EXPO_HOST='localhost'
```

Этот режим возвращает API к loopback и использует `--localhost` для Expo. QR сохраняется, но loopback URL предназначен для локального клиента/настроенного эмулятора, а не другого физического устройства.

## Другие варианты

| Вариант | Когда полезен | Ограничение |
| --- | --- | --- |
| Отдельная консоль Expo — реализовано | QR, горячие клавиши, управление всем проектом через основной BAT | Вывод Expo виден в своей консоли, а не в старом frontend.log |
| Expo в основном терминале | Ручной запуск `npm run start -- --web --lan --port 8081 --clear` | API/БД и завершение процессов придётся обслуживать отдельно |
| Expo tunnel | LAN недоступна или Wi-Fi изолирует устройства | Требуется Internet и tunnel provider; tunnel Metro не публикует ASP.NET API |

Tunnel не включается автоматически и не добавляет зависимости. Для ручного варианта остановите управляемый launcher Expo, подготовьте отдельно доступный с телефона API и запустите Expo в другом терминале:

```powershell
cd tenant-app-frontend
$env:CI = $null
$env:EXPO_OFFLINE = $null
$env:EXPO_PUBLIC_API_URL = 'https://your-development-api.example/api/v1'
npm run start -- --web --tunnel --port 8081 --clear
```

Замените пример API действительным development endpoint. Expo может запросить установку tunnel provider; этот шаг не выполняется BAT автоматически. API tunnel/hosting, доверенные origins и сетевой доступ настраиваются отдельно. `CUBE_EXPO_HOST=tunnel` намеренно не поддерживается launcher, чтобы не создавать видимость доступа к API через tunnel Metro.

## Проверки

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-windows-launcher.ps1
```

Скрипт загружает только определения функций из BAT, не выполняя установку зависимостей и запуск проекта. Проверяются синтаксис payload/worker, socket/database port logic, LAN/local/fallback, валидация IP, process options без redirect для Expo, сохранение backend logs, quoting/UTF-8 BOM и cleanup обеих process trees/owned database. Для него добавлен Windows PowerShell CI job.

Дополнительно в Linux TTY запущен настоящий установленный Expo CLI с `--web --lan`: QR отображается, **c** выводит его повторно, `/status`, Android manifest и launch bundle возвращают 200. В native bundle подтверждён встроенный LAN API URL. Адрес для этой проверки — тестовый `192.168.1.20`, порт 18081. Expo сообщил о timeout получения внешней schema manifest assets; проверка иконок/шрифтов на устройстве не выполнялась. Полный запуск BAT с Windows Docker Desktop и сканирование физическим устройством этой локальной проверкой не подтверждаются.
