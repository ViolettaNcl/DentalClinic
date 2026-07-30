[⬅ Назад в README](../README.md)

*[🇬🇧 English version](en/DEPLOYMENT.md)*

# 🚀 Развёртывание (Deployment)

## 1. Публикация сборки

```bash
dotnet publish -c Release -o ./publish
```

Папка `./publish` содержит всё необходимое для запуска: скомпилированное приложение,
`wwwroot/` со статикой, `appsettings.json` (не забудьте — на сервере он должен содержать
**реальные продовые значения**, заданные не через файл, а через переменные окружения —
см. ниже).

## 2. Конфигурация через переменные окружения (рекомендуется для прод)

ASP.NET Core позволяет переопределить любое значение из `appsettings.json` переменной
окружения с именем через `__` вместо `:`, например:

```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=..."
export Jwt__Key="случайная-длинная-строка-минимум-32-символа"
export Gemini__ApiKey="..."
```

Это самый безопасный способ хранить секреты на сервере — они не лежат в файле рядом с кодом.

## 3. Варианты хостинга

Проект — обычное ASP.NET Core приложение, поэтому подходит любой из вариантов:

| Вариант | Особенности |
|---|---|
| IIS / Windows Server | Классический вариант для .NET, нужен модуль ASP.NET Core Hosting Bundle |
| Docker-контейнер | Используйте `dotnet publish` + официальный образ `mcr.microsoft.com/dotnet/aspnet:9.0` |
| Linux + Nginx (обратный прокси) | `dotnet DentalClinic.dll` как systemd-сервис за Nginx |
| Managed-хостинги (Azure App Service, Railway, Render и т.п.) | Обычно достаточно указать переменные окружения в панели хостинга |

В проекте уже есть готовый профиль публикации по FTP
(`Properties/PublishProfiles/FTPProfile.pubxml`) — он использовался для хостинга
`somee.com`. **Файл `FTPProfile.pubxml.user` может содержать сохранённые учётные данные
FTP — проверьте это перед публикацией репозитория** (см. `docs/SECURITY.md`).

## 4. Настройка HTTPS/HSTS

В `Program.cs` `UseHsts()` и `UseHttpsRedirection()` включаются только вне режима
Development — на реальном сервере с валидным TLS-сертификатом (например, через Let's
Encrypt / Nginx / хостинг-провайдера) это будет работать «из коробки».

## 5. База данных на проде

1. Создайте БД на вашем сервере SQL Server (или используйте managed-сервис).
2. Укажите строку подключения через переменную окружения (см. п.2).
3. Примените миграции:
   ```bash
   dotnet ef database update --connection "ваша_прод_строка_подключения"
   ```
   либо выполните это один раз локально с временно указанной прод-строкой подключения.

## 6. Автоматический деплой (CI/CD)

Кроме `ci.yml` (сборка на каждый push/PR) в проекте есть `.github/workflows/cd.yml` —
он запускается автоматически после успешного CI на ветке `main` и делает две
независимые вещи:

| Job | Что делает | Какие секреты нужны в Settings → Secrets and variables → Actions |
|---|---|---|
| `deploy-ftp` | `dotnet publish` + заливка результата по FTPS на текущий хостинг (somee.com) | `FTP_SERVER`, `FTP_USERNAME`, `FTP_PASSWORD`, `FTP_SERVER_DIR` (например `/www.Dental-Clinic.somee.com`) |
| `docker-image` | Сборка Docker-образа и публикация в `ghcr.io/<владелец репозитория>/dentalclinic` | Не нужны — использует встроенный `GITHUB_TOKEN` |

Если секреты FTP не заданы, `deploy-ftp` просто упадёт с ошибкой на шаге загрузки —
репозиторию и текущему хостингу это не навредит. Job `docker-image` работает
независимо и всегда публикует свежий образ, даже если FTP-деплой не настроен.

Запустить оба job вручную (без ожидания push) можно через
Actions → CD → Run workflow (`workflow_dispatch`).

## 7. Запуск через Docker / docker-compose

В проекте есть `Dockerfile` (multi-stage build) и `docker-compose.yml`, который
поднимает приложение вместе с SQL Server — не нужно ставить SQL Server локально.

```bash
cp .env.example .env
# откройте .env и впишите свои значения (пароль БД, JWT-ключ, ключ Gemini API)

docker compose up --build
```

Приложение будет доступно на `http://localhost:8080`, SQL Server — на `localhost:1433`.

**Важно:** миграций EF Core в проекте нет — таблицы в БД создаются вручную
(тем же способом, что вы уже используете для somee.com). При первом запуске
контейнера с пустой БД `DbSeeder` заполнит `Services`/`Doctors` только если
таблицы уже существуют — создайте схему до первого старта `app`.

Данные SQL Server и загруженные аватары хранятся в именованных Docker-томах
(`dentalclinic-db-data`, `dentalclinic-uploads`) — переживают `docker compose down`
(но не `docker compose down -v`).

## 8. Чек-лист перед деплоем

- [ ] `appsettings.json` в репозитории не содержит реальных секретов (см. `docs/SECURITY.md`)
- [ ] `Jwt:Key` на проде — новый случайный ключ, отличный от использовавшегося в разработке
- [ ] `AllowedOrigins` указывает на реальный домен фронтенда, а не на `localhost`
- [ ] `BackgroundJobs:CleanupEnabled` осознанно включён/выключен под ваш процесс
- [ ] Резервное копирование БД настроено на стороне хостинг-провайдера
- [ ] Если используете `cd.yml` — секреты `FTP_SERVER`/`FTP_USERNAME`/`FTP_PASSWORD`/`FTP_SERVER_DIR` заданы в Settings → Secrets and variables → Actions
- [ ] Если используете Docker — таблицы в БД контейнера созданы вручную ДО первого запуска `app` (миграций нет)
