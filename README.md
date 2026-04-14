# BrokerFlow — Инструмент обработки отчётов брокеров

## Описание

BrokerFlow — это веб-приложение для обработки отчётов различных брокеров (остатки денежных средств, остатки ценных бумаг, сделки) и их конвертации в XML-формат для учётных систем.

### Возможности

- **Парсинг отчётов** в форматах XML, CSV, XLS, XLSX, PDF
- **Сетевые папки** — чтение отчётов из сетевых шар (\\\\server\\share)
- **Маски файлов** — фильтрация файлов по шаблонам (*.xml, report_*.csv)
- **Визуальный маппинг** — настройка соответствия полей отчёта → XML
- **Выражения** — арифметические (+, -, *, /), строковые (concat, upper, trim, substr, regex), логические (AND, OR, NOT), условные (IF/THEN/ELSE)
- **GUID-генерация** — создание GUID на основе поля отчёта
- **Разность дат** — вычисление разности дат из полей отчёта
- **Сумма/вычитание дат** — прибавление дней/месяцев к дате
- **Проверка удалённых записей** — если запись отсутствует в текущем отчёте
- **Разделение файлов** — генерация отдельного XML на каждую строку по условиям
- **CSV-разделители** — настройка сепаратора для CSV-файлов
- **XML-шаблоны** — импорт или создание целевой XML-структуры
- **Расписание** — автоматическая обработка по CRON-расписанию
- **Аудит** — полный журнал операций

### Стек технологий

| Компонент | Технология |
|-----------|------------|
| Backend | C# / ASP.NET Core 8 |
| ORM | Entity Framework Core 8 |
| СУБД | Microsoft SQL Server (внешний) |
| Планировщик | Quartz.NET |
| Real-time | SignalR |
| Frontend | HTML/CSS/JS (SPA, встроено в wwwroot) |
| Хостинг | Windows Server, IIS или Windows Service |

---

## Требования к серверу

### Минимальные требования

- **ОС**: Windows Server 2019 / 2022
- **CPU**: 2 ядра
- **RAM**: 4 GB
- **Диск**: 10 GB свободного пространства
- **.NET**: .NET 8.0 Runtime (или SDK для сборки)
- **СУБД**: MS SQL Server 2019+ (может быть на отдельном сервере)

### Программное обеспечение

1. **.NET 8.0 Runtime** (ASP.NET Core Runtime)
   - Скачать: https://dotnet.microsoft.com/download/dotnet/8.0
   - Выбрать: *ASP.NET Core Runtime 8.0.x — Windows — Hosting Bundle*

2. **MS SQL Server** (если нет установленного)
   - SQL Server 2019/2022 Express (бесплатный) или Standard/Enterprise
   - SQL Server Management Studio (SSMS) — для управления БД

3. **sqlcmd** (для автоматической инициализации БД)
   - Устанавливается вместе с SSMS или отдельно

---

## Инструкция по разворачиванию

### Вариант 1: Автоматическая установка (рекомендуется)

#### Шаг 1. Скопировать проект на сервер

Скопируйте папку `BrokerFlow` на сервер, например в `C:\Projects\BrokerFlow`.

#### Шаг 2. Запустить скрипт установки

Откройте **PowerShell от имени администратора** и выполните:

```powershell
# С Windows-аутентификацией (Trusted Connection)
cd C:\Projects\BrokerFlow\Scripts
.\Install-BrokerFlow.ps1 -TrustedConnection -InstallAsService

# Или с SQL-аутентификацией
.\Install-BrokerFlow.ps1 `
    -SqlServer "SQL-SERVER-NAME" `
    -SqlDatabase "BrokerFlow" `
    -SqlUser "brokerflow_user" `
    -SqlPassword "YourPassword123!" `
    -Port 5000 `
    -InstallAsService
```

Параметры скрипта:

| Параметр | По умолчанию | Описание |
|----------|-------------|----------|
| `-InstallDir` | `C:\BrokerFlow` | Директория установки |
| `-SqlServer` | `localhost` | Адрес SQL Server |
| `-SqlDatabase` | `BrokerFlow` | Имя базы данных |
| `-SqlUser` | — | Логин SQL (пусто = Windows Auth) |
| `-SqlPassword` | — | Пароль SQL |
| `-Port` | `5000` | HTTP-порт приложения |
| `-TrustedConnection` | — | Использовать Windows-аутентификацию |
| `-InstallAsService` | — | Установить как службу Windows |

#### Шаг 3. Проверить работу

Откройте в браузере:
- **UI**: http://localhost:5000
- **Swagger**: http://localhost:5000/swagger
- **Health**: http://localhost:5000/api/health

---

### Вариант 2: Ручная установка

#### Шаг 1. Установить .NET 8 Runtime

```powershell
# Скачать и установить ASP.NET Core 8.0 Hosting Bundle
# https://dotnet.microsoft.com/download/dotnet/8.0
# Выбрать: Hosting Bundle (включает Runtime + IIS модуль)
```

Проверить установку:
```powershell
dotnet --list-runtimes
# Должно отобразить: Microsoft.AspNetCore.App 8.0.x
```

#### Шаг 2. Подготовить базу данных

Подключитесь к SQL Server через SSMS и выполните:

```sql
-- Создать базу данных
CREATE DATABASE [BrokerFlow];
GO

-- Создать пользователя (опционально)
USE [master]
GO
CREATE LOGIN [brokerflow_user] WITH PASSWORD = 'YourStrongPassword123!';
GO
USE [BrokerFlow]
GO
CREATE USER [brokerflow_user] FOR LOGIN [brokerflow_user];
ALTER ROLE [db_owner] ADD MEMBER [brokerflow_user];
GO
```

Или выполните скрипт `Scripts/init-database.sql` целиком.

#### Шаг 3. Собрать приложение

```powershell
cd C:\Projects\BrokerFlow
dotnet publish BrokerFlow.Api\BrokerFlow.Api.csproj `
    -c Release `
    -o C:\BrokerFlow\app `
    --self-contained false
```

#### Шаг 4. Настроить конфигурацию

Отредактируйте файл `C:\BrokerFlow\app\appsettings.Production.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR-SQL-SERVER;Database=BrokerFlow;User Id=brokerflow_user;Password=YourPassword;TrustServerCertificate=True;Encrypt=True;"
  },
  "Paths": {
    "Base": "C:\\BrokerFlow",
    "Reports": "C:\\BrokerFlow\\reports",
    "Output": "C:\\BrokerFlow\\output",
    "Uploads": "C:\\BrokerFlow\\uploads"
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    }
  }
}
```

#### Шаг 5. Создать директории

```powershell
New-Item -ItemType Directory -Force -Path C:\BrokerFlow\reports
New-Item -ItemType Directory -Force -Path C:\BrokerFlow\output
New-Item -ItemType Directory -Force -Path C:\BrokerFlow\uploads
New-Item -ItemType Directory -Force -Path C:\BrokerFlow\logs
```

#### Шаг 6. Открыть порт в файрволе

```powershell
New-NetFirewallRule -DisplayName "BrokerFlow-HTTP-5000" `
    -Direction Inbound -Protocol TCP -LocalPort 5000 `
    -Action Allow -Profile Domain,Private
```

#### Шаг 7. Запустить приложение

**Вариант А — ручной запуск (для тестирования):**

```powershell
cd C:\BrokerFlow\app
$env:ASPNETCORE_ENVIRONMENT = "Production"
.\BrokerFlow.Api.exe
```

**Вариант Б — Windows Service (для продакшена):**

```powershell
# Создать службу
New-Service -Name "BrokerFlowService" `
    -BinaryPathName "C:\BrokerFlow\app\BrokerFlow.Api.exe" `
    -DisplayName "BrokerFlow Report Processor" `
    -Description "Processes broker reports and generates XML for accounting" `
    -StartupType Automatic

# Настроить переменные окружения
$regPath = "HKLM:\SYSTEM\CurrentControlSet\Services\BrokerFlowService"
Set-ItemProperty -Path $regPath -Name "Environment" `
    -Value @("ASPNETCORE_ENVIRONMENT=Production") -Type MultiString

# Запустить
Start-Service -Name "BrokerFlowService"

# Проверить статус
Get-Service -Name "BrokerFlowService"
```

**Вариант В — IIS (для корпоративного окружения):**

1. Установить **ASP.NET Core Hosting Bundle**
2. В IIS Manager создать новый сайт:
   - Physical Path: `C:\BrokerFlow\app`
   - Port: 80 (или другой)
   - Application Pool: No Managed Code, пул с учётной записью, имеющей доступ к SQL и сетевым папкам
3. В web.config (создаётся автоматически при publish):
   ```xml
   <aspNetCore processPath=".\BrokerFlow.Api.exe"
               stdoutLogEnabled="true"
               stdoutLogFile=".\logs\stdout"
               hostingModel="InProcess">
     <environmentVariables>
       <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
     </environmentVariables>
   </aspNetCore>
   ```

---

## Подключение сетевых папок

### Монтирование CIFS/SMB шары

Если отчёты брокеров лежат в сетевой папке, подключите её:

```powershell
# Постоянное подключение сетевого диска
net use Z: \\\\fileserver\\broker-reports /user:DOMAIN\svc_broker Password /persistent:yes

# Или через New-PSDrive
New-PSDrive -Name "BrokerReports" -PSProvider FileSystem `
    -Root "\\\\fileserver\\broker-reports" `
    -Credential (Get-Credential) -Persist
```

В настройках приложения укажите:
- **Reports Directory**: `Z:\` или `\\\\fileserver\\broker-reports`

> **Важно**: При запуске как Windows Service, служба работает под учётной записью `Local System`, которая НЕ имеет доступа к сетевым ресурсам. Необходимо:
> 1. Создать доменную сервисную учётную запись
> 2. Дать ей права на сетевую папку
> 3. Изменить учётную запись службы: `Services.msc` → BrokerFlowService → Log On → This account

---

## Настройка маппинга

### Типы выражений

| Тип | Описание | Пример JSON |
|-----|----------|-------------|
| `field` | Значение поля | `{"type":"field","name":"TradeId"}` |
| `literal` | Константа | `{"type":"literal","value":"RUB"}` |
| `arithmetic` | Математика | `{"type":"arithmetic","op":"*","left":{"type":"field","name":"Price"},"right":{"type":"field","name":"Qty"}}` |
| `string_op` | Строковые | `{"type":"string_op","op":"upper","value":{"type":"field","name":"Currency"}}` |
| `conditional` | Условие | `{"type":"conditional","condition":{...},"then":{...},"else":{...}}` |
| `guid` | GUID из поля | `{"type":"guid","source":{"type":"field","name":"TradeId"}}` |
| `date_diff` | Разность дат | `{"type":"date_diff","date_start":{"type":"field","name":"StartDate"},"date_end":{"type":"field","name":"EndDate"},"unit":"days"}` |
| `compare` | Сравнение | `{"type":"compare","op":"==","left":{"type":"field","name":"Type"},"right":{"type":"literal","value":"BUY"}}` |
| `logical` | Логика | `{"type":"logical","op":"and","operands":[...]}` |

### Пример сложного маппинга

Задача: *если поле `Direction` = "1" и поле `Status` = "EX", то взять `Amount` + `Commission`, иначе `Amount`*

```json
{
  "xml_path": "Trade/TotalAmount",
  "expression": {
    "type": "conditional",
    "condition": {
      "type": "logical",
      "op": "and",
      "operands": [
        {"type": "compare", "op": "==", "left": {"type": "field", "name": "Direction"}, "right": {"type": "literal", "value": "1"}},
        {"type": "compare", "op": "==", "left": {"type": "field", "name": "Status"}, "right": {"type": "literal", "value": "EX"}}
      ]
    },
    "then": {
      "type": "arithmetic", "op": "+",
      "left": {"type": "field", "name": "Amount"},
      "right": {"type": "field", "name": "Commission"}
    },
    "else": {"type": "field", "name": "Amount"}
  }
}
```

### Разделение на файлы

Условие: *на каждую строку с уникальным ID, где поле не начинается с "R" и дата = сегодня*

```json
{
  "splitOutput": true,
  "splitFileNamePattern": "trade_{TradeId}_{_date}.xml",
  "splitCondition": {
    "type": "logical",
    "op": "and",
    "operands": [
      {"type": "compare", "op": "not_like", "left": {"type": "field", "name": "Code"}, "right": {"type": "literal", "value": "R%"}},
      {"type": "compare", "op": "==", "left": {"type": "field", "name": "TradeDate"}, "right": {"type": "literal", "value": "{{TODAY}}"}}
    ]
  }
}
```

---

## API-документация

Swagger UI доступен по адресу: `http://your-server:5000/swagger`

Основные эндпоинты:

| Метод | URL | Описание |
|-------|-----|----------|
| GET | /api/health | Проверка состояния |
| GET/POST | /api/sources | Управление источниками |
| GET/POST | /api/templates | Управление XML-шаблонами |
| POST | /api/templates/build-from-fields | Создание шаблона из полей |
| GET/POST | /api/mappings | Управление маппингами |
| POST | /api/mappings/preview | Предпросмотр маппинга |
| POST | /api/files/upload | Загрузка файла |
| GET | /api/files/scan | Сканирование директории |
| POST | /api/files/parse | Парсинг и предпросмотр |
| GET/POST | /api/jobs | Управление задачами |
| GET/POST | /api/schedules | Управление расписаниями |
| GET/PUT | /api/config | Настройки |
| GET | /api/audit | Журнал аудита |

---

## Обслуживание

### Логи

При запуске как Windows Service, логи пишутся в:
- **stdout**: Event Viewer → Windows Logs → Application
- **Настроить**: в `appsettings.Production.json` → `Logging`

Для IIS: `C:\BrokerFlow\app\logs\stdout_*.log`

### Обновление

```powershell
# Остановить службу
Stop-Service -Name "BrokerFlowService"

# Собрать новую версию
cd C:\Projects\BrokerFlow
git pull
dotnet publish BrokerFlow.Api\BrokerFlow.Api.csproj `
    -c Release -o C:\BrokerFlow\app --self-contained false

# Запустить
Start-Service -Name "BrokerFlowService"
```

### Резервное копирование

```powershell
# Бэкап базы данных
BACKUP DATABASE [BrokerFlow]
TO DISK = 'C:\Backups\BrokerFlow_backup.bak'
WITH FORMAT, COMPRESSION;

# Бэкап конфигурации
Copy-Item "C:\BrokerFlow\app\appsettings.Production.json" "C:\Backups\"
```

---

## Устранение проблем

| Проблема | Решение |
|----------|---------|
| Не запускается | Проверить `.NET 8 Runtime`: `dotnet --list-runtimes` |
| Ошибка подключения к БД | Проверить строку подключения, доступность SQL Server, firewall |
| Нет доступа к сетевой папке | Изменить учётную запись службы на доменную |
| Порт занят | Изменить порт в `appsettings.Production.json` → `Kestrel:Endpoints:Http:Url` |
| 500 Internal Server Error | Смотреть логи: Event Viewer или `C:\BrokerFlow\app\logs\` |
| Swagger не открывается | Swagger доступен только в Development. Для Production добавить `app.UseSwagger()` без условия |
