# Кабинет арендатора — фронтенд (React Native + Expo)

## Установка в ваш проект tenant-app
1. Остановите `expo start` (Ctrl+C).
2. Удалите всё содержимое папки `tenant-app/src` и скопируйте туда папки из `src` этого архива.
3. Установите зависимости (в папке tenant-app):
   npm i @tanstack/react-query react-hook-form
   npx expo install expo-secure-store expo-image-picker expo-document-picker expo-web-browser @expo/vector-icons
4. В `app.json` замените `"userInterfaceStyle": "automatic"` на `"light"` (тема приложения светлая).
5. Запуск: `npx expo start -c`

## Вход в тестовом режиме
Логин `demo`, пароль `demo1234`. Пока не задан адрес сервера, приложение работает на тестовых данных
(`src/api/mock.ts`): созданные обращения, показания и оплаты видны до перезапуска.

## Подключение к настоящему серверу
Скопируйте `.env.example` в `.env` и укажите адрес: `EXPO_PUBLIC_API_URL=https://ваш-сервер/api`.
Список запросов (контракт для бэкенда) — в `src/api/index.ts`. Экраны менять не нужно.

## Структура
- `src/app` — экраны (expo-router)
- `src/ui` — общие компоненты (кнопки, поля, карточки, загрузка/ошибки/уведомления)
- `src/theme` — цвета и отступы («Светлый премиум»)
- `src/api` — клиент сервера, типы, тестовые данные
- `src/features` — повторно используемые куски (счета, оплата)
