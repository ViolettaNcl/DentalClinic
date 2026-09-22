# Руководство разработчика

[Документация](README.ru.md) · [README проекта](../README.ru.md) · [English](en/DEVELOPER_GUIDE.md)

## Требования

Нужны .NET SDK 10.x, Git и доступная SQL Server 2019+ / Azure SQL. Для JS- и Playwright-тестов рекомендуется Node.js 22; Docker Desktop нужен только для контейнерного варианта запуска.

Ключи Gemini и ElevenLabs не требуются для большинства локальных задач. Они нужны при проверке соответствующих функций генерации, перевода и озвучивания.

## Получение и настройка

```bash
git clone https://github.com/ViolettaNcl/DentalClinic.git
cd DentalClinic
dotnet tool restore
dotnet restore
```

Создайте локальную конфигурацию, исключённую из Git:

```powershell
Copy-Item appsettings.Example.json appsettings.json
```

В Bash используйте `cp appsettings.Example.json appsettings.json`. Замените заглушки: как минимум нужны доступная БД, случайный JWT-ключ длиной не менее 32 UTF-8 байт, издатель и аудитория токенов.

Для секретов предпочтителен ASP.NET Core Secret Manager. Следующие значения — только примеры, не готовые пароли:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=..."
dotnet user-secrets set "Jwt:Key" "generate-a-long-random-development-secret"
dotnet user-secrets set "Jwt:Issuer" "DentalClinicLocal"
dotnet user-secrets set "Jwt:Audience" "DentalClinicLocalClient"
dotnet user-secrets set "Gemini:ApiKey" "development-key-if-needed"
```

`user-secrets init` добавляет идентификатор в файл проекта. Не включайте это изменение в коммит, если команда не договорилась об общем идентификаторе.

## База данных и запуск

Версия EF CLI закреплена в `.config/dotnet-tools.json`.

```bash
dotnet ef migrations list --no-connect
dotnet ef database update
dotnet run
```

Профили запуска: `http://localhost:5192` и `https://localhost:7063`. Swagger доступен по `/swagger` в Development, проверка здоровья — по `/health`.

При запуске с реляционной БД приложение также применяет миграции до обслуживания запросов. `DbSeeder` заполняет начальные данные врачей, услуг и запись базы знаний о седации. Учётные записи не создаются: первого супер-администратора владелец среды подготавливает отдельно.

Development отключает production-ограничения частоты запросов и допускает некоторые запросы инструментов без заголовков происхождения. Такой запуск не доказывает корректность всех production-проверок безопасности.

## Docker

```powershell
Copy-Item .env.example .env
# Замените заглушки; .env не должен попасть в Git.
docker compose up --build
```

Compose запускает приложение и SQL Server 2022 Express. Сайт доступен по `http://localhost:8080`.

```bash
docker compose ps
docker compose logs -f app
docker compose down
```

Не добавляйте `-v` к остановке, если данные нужно сохранить: этот параметр удаляет именованные тома.

## Карта изменений

| Задача | Где искать |
|---|---|
| Маршруты, права, DTO | `Controllers/`, `Models/`, справочник API |
| Бизнес-правила и конкурентные операции | `Services/`, модульные и интеграционные тесты |
| Схема и индексы | `Models/`, `Data/ApplicationDbContext.cs`, `Migrations/`, словарь данных |
| Конвейер и защита запросов | `Program.cs`, `Middleware/`, `Filters/` |
| Уведомления | `Hubs/`, `NotificationService`, `wwwroot/assets/js/services/realtime.js` |
| Служебные задачи | `BackgroundJobs/`, служба обслуживания заявок, maintenance-контроллер, `vercel.json` |
| Публичные страницы | `wwwroot/index.html`, `wwwroot/pages/`, CSS и менеджеры страниц |
| Кабинеты | `wwwroot/assets/js/managers/{patient,admin}/`, HTML/CSS кабинетов |
| Общий браузерный код | `wwwroot/assets/js/core/`, `services/` |
| Переводы | `wwwroot/assets/i18n/{ru,en,fr,el,ar}.json` |

Подробности — в [архитектуре](ARCHITECTURE.md).

## Изменение схемы

В текущем репозитории есть явные классы миграций, но нет отслеживаемого снимка модели EF. Автоматическое создание миграции без корректной исходной модели может повторно сгенерировать создание таблиц. Сначала проверьте стратегию миграций и согласуйте снимок с существующей схемой, затем применяйте стандартный процесс:

```bash
dotnet ef migrations add DescriptiveMigrationName
dotnet ef migrations list --no-connect
dotnet ef database update
```

Перед применением к общей БД проверяйте операции и SQL, сохранность данных, ограничения, индексы и поведение `Down`. Несовместимые старые данные нужно разбирать явно, а не молча удалять ради успешного выпуска. Укажите требования к резервной копии в PR.

## Сессии и API-инструменты

JWT передаётся в `dc_auth` с `HttpOnly`. Не возвращайте его в JSON и не переносите в local/session storage, URL или журналы.

Браузер использует запросы того же происхождения с cookie. Production-операции требуют корректного `Origin`/`Referer`; ИИ-маршруты требуют допустимый `Origin`.

[Postman](DentalClinic.postman_collection.json) использует cookie jar, а не токен из JSON. Вход пациента и администратора заменяет одну и ту же cookie. Подставляйте реальные локальные идентификаторы и будущие даты; не запускайте изменяющие данные примеры на production.

## Тесты

```bash
dotnet test DentalClinic.Tests/DentalClinic.Tests.csproj --configuration Release
npm install
npm run test:js
```

`CustomWebApplicationFactory` запускает конвейер приложения с уникальной EF InMemory БД. Эти проверки не воспроизводят блокировки SQL Server, фильтрованные индексы и ограничения БД. Изменения конкурентного доступа и схемы требуют отдельных проверок с SQL Server.

Для Playwright в PowerShell:

```powershell
npx playwright install chromium
$env:BASE_URL = 'http://localhost:5192'
npm run test:e2e
```

В Bash: `BASE_URL=http://localhost:5192 npm run test:e2e`.

Без `BASE_URL` тесты направлены на публичный сайт. Некоторые проверки канонического URL ожидают production-домен даже при другом адресе, поэтому локальный запуск не является полностью независимым от окружения. Не запускайте разрушительные сценарии на production без отдельного разрешения.

CI также собирает контейнер и проверяет обнаружение миграций. Отдельные процессы предусмотрены для CodeQL, опубликованного сайта, Playwright, Gemini и хранения образов Vercel.

## Типовые задачи

При добавлении API-маршрута используйте ограниченный DTO, проверяйте роль и владельца, выносите общую логику в сервис, защищайте важные инварианты в БД. Покройте успешный запрос, валидацию, отсутствие прав, конфликт и отмену; обновите обе версии справочника API.

При изменении текста интерфейса обновляйте одинаковые ключи всех пяти языков и проверяйте арабскую раскладку, а не только наличие перевода.

Для нового платного вызова провайдера нужны серверный ключ, ограничения тела, тайм-ауты/отмена, безопасные журналы, проверка происхождения, локальные и распределённые квоты, понятный отказ и тесты на утечку ключа.

## Решение проблем

| Симптом | Проверка |
|---|---|
| Не хватает строки подключения или JWT-ключа | Проверьте действующую конфигурацию; переменные окружения используют `__` |
| Ошибка HTTPS-сертификата | Выполните `dotnet dev-certs https --trust` либо используйте HTTP только локально |
| Команда `dotnet ef` не найдена | Выполните `dotnet tool restore` в корне проекта |
| Чат или перевод не отвечает | Проверьте ключ Gemini, квоту, Origin и ограничения запроса |
| Вход работает в браузере, но не в Postman | Проверьте cookie jar, адрес/порт и Origin; токена в JSON нет |
| Миграция не проходит | Проверьте сеть, TLS, права, историю миграций и ограничения данных; не удаляйте production-данные |

Перед PR выполните [CONTRIBUTING.md](../CONTRIBUTING.md), обновите документацию на обоих языках и перечитайте [безопасность](SECURITY.md), если меняются сессии, персональные данные, загрузки, провайдеры или развёртывание.
