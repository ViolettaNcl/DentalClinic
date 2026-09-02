[⬅ Назад в README](../README.md)

*[🇬🇧 English version](en/DEPLOYMENT.md)*

# 🚀 Развёртывание в Vercel

Проект запускается в Vercel как ASP.NET Core 9 container service. Конфигурация
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

## 2. База данных и уход от прежнего хостинга

Vercel запускает приложение, но не предоставляет SQL Server внутри контейнера.
Нужен внешний managed SQL Server, например Azure SQL. Чтобы полностью отказаться
от прежнего хостинга и сохранить данные:

1. Сделайте резервную копию текущей БД.
2. Восстановите её в новом managed SQL Server.
3. Выполните после резервной копии `scripts/sql/20260901_stage1_appointments.sql`.
4. Укажите новую строку подключения в `ConnectionStrings__DefaultConnection`.
5. Проверьте `GET /health`; ожидаются HTTP 200 и `"status":"Healthy"`.

В репозитории пока нет полного набора EF Core migrations для создания всей схемы
с нуля. `DbSeeder` заполняет врачей и услуги, но ожидает, что таблицы уже созданы.
Не направляйте production на пустую БД без предварительного создания/переноса схемы.

## 3. Деплой из GitHub

Рекомендуемый постоянный процесс:

1. В Vercel выберите **New Project → Import Git Repository**.
2. Подключите `ViolettaNcl/DentalClinic`, ветка Production — `main`, Root Directory — `.`.
3. Добавьте переменные из раздела 1.
4. Запустите Preview deployment и проверьте `/health`, главную страницу, вход,
   создание заявки и микрофон чат-бота.
5. После проверки выполните Production deployment.

`vercel.json` направляет весь трафик в container service `app`. HTTPS выдаётся
Vercel автоматически; `Program.cs` учитывает `X-Forwarded-Proto`, поэтому цикл
HTTPS-редиректов за прокси не возникает.

## 4. Cron-задачи

В `vercel.json` настроены две ежедневные задачи, совместимые с Hobby plan:

| Время (UTC) | Endpoint | Назначение |
|---|---|---|
| `06:00` | `/api/maintenance/reminders` | Напоминания о приёмах следующего дня |
| `06:15` | `/api/maintenance/cleanup` | Отмена старых pending-заявок, только если явно включена |

Vercel передаёт `Authorization: Bearer <CRON_SECRET>`. Без корректного секрета
оба endpoint возвращают 401. В Vercel обычные `BackgroundService` отключаются,
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
Именованные Docker volumes сохраняют БД и загрузки между перезапусками.

## 7. Ограничение файловых загрузок

Аватары пока сохраняются в `wwwroot/uploads/avatars`. Файловая система Vercel
контейнера не является постоянным хранилищем, поэтому аватары могут пропасть после
нового deployment или пересоздания экземпляра. До использования функции в production
нужно перенести её на Vercel Blob или другое object storage.

## 8. Проверка после публикации

- [ ] Production deployment имеет статус READY.
- [ ] `/health` возвращает HTTP 200 и БД имеет статус Healthy.
- [ ] Главная страница и статические ресурсы открываются по HTTPS.
- [ ] Регистрация, вход пациента и вход администратора работают.
- [ ] Новая заявка не принимает прошедшую дату и не допускает конфликт врача.
- [ ] Микрофон запрашивает разрешение браузера и распознанный текст попадает в чат.
- [ ] Cron endpoints без `CRON_SECRET` возвращают 401.
- [ ] `BackgroundJobs__CleanupEnabled` включён только осознанно.
- [ ] Резервное копирование новой БД настроено.
- [ ] Если FTPS backup нужен, все четыре FTP secrets заданы в GitHub.
- [ ] Аватары перенесены во внешнее object storage либо временно отключены.
