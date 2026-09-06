[⬅ Назад в README](../README.md)

*[🇬🇧 English version](en/DEPLOYMENT.md)*

# 🚀 Развёртывание в Vercel

Проект запускается в Vercel как ASP.NET Core 10 container service. Конфигурация
находится в `Dockerfile.vercel` и `vercel.json`; Vercel завершает TLS на своём
прокси и передаёт приложению назначенный порт через `$PORT`.

Целевой production-домен:

`https://dental-clinic-vn.vercel.app`

## 1. Обязательные production-переменные

Добавьте их в Vercel → Project → Settings → Environment Variables для
Production и Preview. Секреты нельзя добавлять в `vercel.json` или коммитить в Git.

| Переменная | Назначение | Обязательна |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | Строка подключения к внешнему SQL Server | да |
| `Jwt__Key` | Случайный ключ подписи JWT, минимум 32 символа | да |
| `Jwt__Issuer` | Например `DentalClinic` | да |
| `Jwt__Audience` | Например `DentalClinicClient` | да |
| `CRON_SECRET` | Случайный секрет для защищённых Vercel Cron endpoints | да |
| `Gemini__ApiKey` | Ключ чат-бота | для AI-чата |
| `ElevenLabs__ApiKey` | Ключ озвучивания ответов | необязательно |
| `ElevenLabs__VoiceId` | Идентификатор голоса | необязательно |
| `Scheduling__TimeZoneId` | Часовой пояс клиники, по умолчанию `Europe/Moscow` | рекомендуется |
| `BackgroundJobs__CleanupEnabled` | `true` только если автоотмена старых заявок нужна | необязательно |

Для одного домена фронтенд и API работают same-origin. Если API вызывается с
другого домена, добавьте `AllowedOrigins__0=https://ваш-домен`.

## 2. База данных и миграции схемы

Vercel запускает приложение, но не предоставляет SQL Server внутри контейнера.
Нужен внешний managed SQL Server, например Azure SQL.

Цепочка EF migrations рассчитана на безопасное обновление существующей live-схемы:
baseline migration идемпотентна, а последующие migrations добавляют поля и индексы,
которые использует текущая production-версия. При каждом старте приложения с
реляционной БД `Program.cs` теперь выполняет `Database.MigrateAsync()` **до**
`DbSeeder` и до начала обслуживания запросов. Это правило действует и в Vercel,
поэтому новый image больше не может молча запуститься поверх устаревшей схемы.

Перед первым автоматическим обновлением существующей production-БД:

1. Сделайте и проверьте резервную копию БД.
2. Укажите managed SQL Server в `ConnectionStrings__DefaultConnection`.
3. Выполните deployment — pending EF migrations применятся при старте приложения.
4. Проверьте `GET /health`; ожидаются HTTP 200 и `"status":"Healthy"`.
5. Убедитесь, что последняя migration записана в `__EFMigrationsHistory`.

Для локального или ручного maintenance-window по-прежнему можно запускать
`dotnet ef database update`. Не применяйте изменения схемы без резервной копии.

## 3. Деплой из GitHub

Рекомендуемый постоянный процесс:

1. В Vercel выберите **New Project → Import Git Repository**.
2. Подключите `ViolettaNcl/DentalClinic`, ветка Production — `main`, Root Directory — `.`.
3. Добавьте переменные из раздела 1.
4. Запустите Preview deployment и проверьте `/health`, главную страницу, вход,
   создание заявки и микрофон чат-бота.
5. После проверки выполните Production deployment.

`vercel.json` направляет весь трафик в container service `web`. HTTPS выдаётся
Vercel автоматически; `Program.cs` учитывает forwarded protocol/client information
до rate limiting и обработки запросов, зависящих от аутентификации.

## 4. Cron-задачи

В `vercel.json` настроены четыре ежедневные maintenance-задачи:

| Время (UTC) | Endpoint | Назначение |
|---|---|---|
| `06:00` | `/api/maintenance/reminders` | Напоминания о приёмах следующего дня |
| `06:10` | `/api/maintenance/follow-ups` | Одноразовые follow-up уведомления после визита |
| `06:15` | `/api/maintenance/cleanup` | Отмена старых pending-заявок, только если явно включена |
| `06:30` | `/api/maintenance/chat-retention` | Удаление устаревших сообщений и IP-псевдонимов чата |

Vercel передаёт `Authorization: Bearer <CRON_SECRET>`. Без корректного секрета
maintenance endpoints возвращают 401. В Vercel обычные `BackgroundService` отключаются,
потому что контейнер может приостанавливаться между запросами.

## 5. GitHub Actions и резервный FTPS

- `.github/workflows/ci.yml` собирает приложение и запускает тесты на push/PR.
- `.github/workflows/codeql.yml` выполняет анализ C# и JavaScript.
- `.github/workflows/cd.yml` повторно запускает тесты, публикует Docker-образ в GHCR
  и сохраняет резервный FTPS-deploy на Somee.
- Основной production-хостинг — Vercel. FTPS нужен только как резервный путь публикации,
  пока переход на Vercel окончательно не проверен.

Для FTPS job используются только GitHub Secrets/Environment Secrets; значения не
хранятся в репозитории:

- `FTP_SERVER`
- `FTP_USERNAME`
- `FTP_PASSWORD`
- `FTP_SERVER_DIR`

Если хотя бы один из них отсутствует, workflow явно помечает FTPS как пропущенный,
но CI, Vercel и публикация Docker-образа продолжают работать.

После подключения Git Integration Vercel самостоятельно создаёт Preview для веток
и Production deployment для `main`.

## 6. Локальный Docker

```bash
cp .env.example .env
# заполните пароль БД, JWT-ключ и API-ключи
docker compose up --build
```

Приложение доступно на `http://localhost:8080`, SQL Server — на `localhost:1433`.
Именованные Docker volumes сохраняют БД между перезапусками.

## 7. Сохранение аватаров

Новые аватары пациентов и администраторов хранятся в SQL и выдаются через
аутентифицированный avatar endpoint. Legacy-файлы удаляются безопасно при замене
или удалении. Новые загрузки больше не зависят от непостоянной файловой системы
Vercel-контейнера.

## 8. Проверка после публикации

- [ ] Production deployment имеет статус READY.
- [ ] `/health` возвращает HTTP 200 и БД имеет статус Healthy.
- [ ] Pending EF migrations применены, а `__EFMigrationsHistory` содержит последнюю migration.
- [ ] Главная страница и статические ресурсы открываются по HTTPS.
- [ ] Регистрация, вход пациента и вход администратора работают.
- [ ] Новая заявка не принимает прошедшую дату и не допускает конфликт врача.
- [ ] Микрофон запрашивает разрешение браузера и распознанный текст попадает в чат.
- [ ] Cron endpoints без `CRON_SECRET` возвращают 401.
- [ ] `BackgroundJobs__CleanupEnabled` включён только осознанно.
- [ ] Резервное копирование production-БД настроено.
- [ ] Если FTPS backup нужен, все четыре FTP secrets заданы в GitHub.
- [ ] Загрузка и выдача аватара пациента/администратора переживают новый deployment.
