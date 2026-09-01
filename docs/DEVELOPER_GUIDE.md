[⬅ Назад в README](../README.md)

*[🇬🇧 English version](en/DEVELOPER_GUIDE.md)*

# 👨‍💻 Руководство разработчика

## 1. Требования

- [.NET SDK 9](https://dotnet.microsoft.com/download) (`dotnet --version` → 9.x)
- SQL Server (локально, в Docker, в Azure SQL или другом managed SQL Server)
- Ключ Google Gemini API (для чат-бота) — получить на
  [ai.google.dev](https://ai.google.dev)
- (Опционально) ключ ElevenLabs API — для озвучки ответов бота

## 2. Установка и первый запуск

```bash
git clone https://github.com/ViolettaNcl/DentalClinic.git
cd DentalClinic

# 1. Создайте свой appsettings.json на основе шаблона
cp appsettings.Example.json appsettings.json
# откройте appsettings.json и впишите свои значения (строка подключения к БД, JWT-ключ, ключи API)

# 2. Восстановите зависимости
dotnet restore

# 3. Примените миграции EF Core (создаст таблицы в вашей БД)
dotnet ef database update

# 4. Запустите приложение
dotnet run
```

По умолчанию сайт будет доступен на адресе из `Properties/launchSettings.json`
(обычно `https://localhost:7063` и `http://localhost:5192`). В режиме разработки также
доступен Swagger UI: `https://localhost:7063/swagger`.

При первом запуске на пустой базе `DbSeeder` автоматически заполнит таблицы `Services` и
`Doctors` стартовыми данными — дальше их можно редактировать через панель администратора.

## 3. Работа с секретами (важно!)

**Никогда не коммитьте `appsettings.json` с реальными значениями.** Рекомендуемый способ
для локальной разработки — `dotnet user-secrets`:

```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "ваша_строка_подключения"
dotnet user-secrets set "Jwt:Key" "случайная_длинная_строка"
dotnet user-secrets set "Gemini:ApiKey" "ваш_ключ"
```

Секреты из `user-secrets` автоматически подхватываются `IConfiguration` при разработке и
**не попадают в репозиторий**. Подробнее — в [`docs/SECURITY.md`](SECURITY.md).

## 4. Структура проекта

Смотрите раздел «Структура репозитория» в [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) — там
расписано назначение каждой папки.

Коротко, если нужно добавить новую функциональность:

| Что добавляете | Куда смотреть |
|---|---|
| Новый REST-эндпоинт | `Controllers/` — создайте контроллер или добавьте метод в существующий |
| Новую сущность БД | `Models/` (класс) + `Data/ApplicationDbContext.cs` (`DbSet<>`, индексы) + миграция |
| Бизнес-логику / интеграцию | `Services/` |
| Фоновую задачу | `BackgroundJobs/` — наследник `BackgroundService`, зарегистрировать в `Program.cs` |
| Страницу фронтенда | `wwwroot/pages/*.html` + стили в `wwwroot/assets/css/pages/` + логика в `wwwroot/assets/js/managers/` |
| Перевод интерфейса | добавьте ключ во все файлы `wwwroot/assets/i18n/*.json` |

## 5. Миграции базы данных

После изменения модели данных (класс в `Models/` или `ApplicationDbContext`):

```bash
dotnet ef migrations add НазваниеИзменения
dotnet ef database update
```

Требуется установленный инструмент `dotnet-ef`:

```bash
dotnet tool install --global dotnet-ef
```

## 6. Фронтенд: архитектура JS

Фронтенд — без сборщиков и фреймворков, обычные ES-модули:

- `assets/js/core/` — сквозная инфраструктура: i18n, чат-бот, переключатель языка,
  навигация, уведомления в шапке;
- `assets/js/services/` — низкоуровневые сервисы: `apiClient.js` (обёртка над `fetch` с
  JWT), `realtime.js` (обёртка над SignalR-клиентом), `dateUtils.js`;
- `assets/js/managers/` — логика конкретных страниц, разделена по ролям:
  `public/` (публичные страницы), `patient/` (кабинет пациента), `admin/` (панель админа).

Стили организованы по методологии, близкой к ITCSS: `base/` (переменные, сброс) →
`layout/` (шапка/подвал) → `components/` (переиспользуемые блоки) → `pages/`
(специфика конкретных страниц).

## 7. Тестирование локально

### Автотесты

Проект `DentalClinic.Tests` (xUnit) запускается без реальной БД —
интеграционные тесты используют EF Core InMemory вместо SQL Server:

```bash
dotnet test DentalClinic.Tests/DentalClinic.Tests.csproj
```

Что покрыто:
- `Unit/JwtTokenServiceTests` — выпуск JWT, claims, обработка отсутствующего `Jwt:Key`;
- `Unit/GeminiTranslateLimiterTests` — что лимитер реально не даёт двум вызовам
  выполняться параллельно;
- `Integration/HealthEndpointTests` — `/health` отвечает 200 и не требует авторизации;
- `Integration/AuthControllerTests` — регистрация/вход, дубликат email, короткий
  пароль, неверный пароль.

CI (`.github/workflows/ci.yml`) гоняет этот же набор на каждый push/PR в `main`.
Добавляя новый контроллер или сервис — заводите тесты рядом, по той же схеме:
`CustomWebApplicationFactory` уже поднимает всё приложение целиком с in-memory БД.

### Ручная проверка

Дополнительно к автотестам, при заметных изменениях в UI полезно:
1. Проверить эндпоинты через Swagger UI (`/swagger`) или файл `DentalClinic.http`
   (можно открыть и выполнять запросы прямо в Visual Studio / VS Code с расширением REST Client).
2. Проверить UI вручную в браузере для всех трёх ролей (гость, пациент, администратор).

## 8. Устранение типичных проблем

**Приложение не запускается, ошибка «Jwt:Key не задан в конфигурации».**
Вы не создали `appsettings.json` (или не задали ключ через `user-secrets`/переменные
окружения). См. раздел 3 выше.

**Ошибка подключения к SQL Server при `dotnet ef database update`.**
Проверьте, что строка подключения в `ConnectionStrings:DefaultConnection` верна и сервер
БД доступен (для локального SQL Server Express обычно
`Server=(localdb)\mssqllocaldb;Database=DentalClinic;Trusted_Connection=True;`).

**AI-чат не отвечает / возвращает ошибку.**
Проверьте, что `Gemini:ApiKey` задан и ключ действителен — без него `ChatController` не
сможет обратиться к Gemini API. Голосовой ответ (TTS) при этом необязателен: без
`ElevenLabs:ApiKey` просто не будет звука, но текстовый ответ бота продолжит работать.

## 9. Расширение функциональности

**Добавить новый язык интерфейса:**
1. Создайте файл `wwwroot/assets/i18n/<код_языка>.json` со всеми ключами по аналогии с `ru.json`.
2. Добавьте язык в список в `languageSwitcher.js`.
3. Если нужны переводы ФИО врачей — добавьте поле `FullName<Код>` в модель `Doctor` и
   таблицу `Doctors` (потребуется миграция).

**Изменить цены без правки кода:**
Все цены — в таблице `Services`, редактируются через панель администратора. AI-бот
подхватывает изменения автоматически, без перезапуска сервера — см. `ChatKnowledgeService`.
