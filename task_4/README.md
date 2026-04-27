# MeetingBooking.Api

## Что это за программа

`MeetingBooking.Api` - учебная веб-служба на C# и ASP.NET Core. Она моделирует процесс бронирования переговорной комнаты, который проходит через несколько шагов и может получать одно и то же событие повторно.

Главная цель работы - показать машину состояний, идемпотентную обработку событий, компенсацию при сбое шага и наблюдаемость сервиса через журналы, health checks и метрики.

## Что требовалось сделать

По заданию нужно было реализовать веб-службу, которая:

* содержит машину состояний минимум из четырех состояний;
* хранит текущее состояние в памяти по ключу процесса;
* принимает события с ключом идемпотентности;
* не меняет состояние при повторной доставке события;
* выполняет компенсацию при сбое следующего шага;
* пишет в журнал переходы, повторы и компенсации;
* добавляет во все журнальные записи сквозной `correlationId`;
* имеет проверку живости и проверку готовности;
* переводит readiness в неуспешное состояние при критической деградации;
* показывает счетчики успешных переходов, ошибок, повторов, компенсаций и задержку по шагам.


## Как устроен проект

```text
Task4Framework.sln
NuGet.Config
src/
  MeetingBooking.Api/
    Program.cs
    Models.cs
    BookingStore.cs
    ProcessLogStore.cs
    MetricsStore.cs
    ReadinessState.cs
    Middleware.cs
    AppException.cs
tests/
  MeetingBooking.Tests/
    Program.cs
README.md
```

`src/MeetingBooking.Api` - сама веб-служба.

`tests/MeetingBooking.Tests` - консольная программа, которая автоматически запускает API, отправляет HTTP-запросы и проверяет основные сценарии.

## Машина состояний

Процесс бронирования хранится по `processKey`. В проекте используются такие состояния:

* `New` - процесс создан, но комната еще не забронирована;
* `RoomReserved` - комната зарезервирована;
* `ParticipantsNotified` - участники уведомлены;
* `EquipmentPrepared` - оборудование подготовлено;
* `Completed` - бронирование завершено;
* `Failed` - состояние оставлено в модели для расширения, но в текущем сценарии сбой закрывается компенсацией.

Основной успешный путь:

```text
New
-> ReserveRoom
-> RoomReserved
-> NotifyParticipants
-> ParticipantsNotified
-> PrepareEquipment
-> EquipmentPrepared
-> ConfirmBooking
-> Completed
```

Если событие не подходит к текущему состоянию, API возвращает ошибку `INVALID_TRANSITION`.

## Идемпотентность

Каждое событие содержит:

* `processKey` - ключ конкретного процесса;
* `idempotencyKey` - ключ конкретной доставки события;
* `eventName` - имя события;
* `correlationId` - сквозной идентификатор для журналов;
* `failStep` - признак искусственного сбоя шага.

Если событие с тем же `idempotencyKey` приходит повторно в рамках того же процесса, состояние не меняется. Сервис возвращает ответ с `duplicate: true`, пишет запись в журнал и увеличивает счетчик повторных доставок.

## Компенсация

Компенсация реализована для сбоя шага `PrepareEquipment`.

Сценарий такой:

1. `ReserveRoom` переводит процесс в `RoomReserved`.
2. `NotifyParticipants` переводит процесс в `ParticipantsNotified`.
3. `PrepareEquipment` с `failStep: true` имитирует сбой следующего шага.
4. Сервис откатывает результат предыдущего шага: уведомление участников считается отмененным.
5. Состояние возвращается в `RoomReserved`.

При этом сервис:

* пишет запись `compensation` в журнал;
* увеличивает счетчик `failedTransitions`;
* увеличивает счетчик `compensations`;
* регистрирует критическую деградацию для readiness.

## Наблюдаемость

В проекте есть журнал последних 100 событий. Его можно получить через:

```text
GET /api/logs
```

В журнал попадают:

* успешные переходы;
* повторные доставки;
* компенсации.

Во всех записях есть `correlationId`, поэтому можно связать ответ API и внутреннее событие сервиса.

Метрики доступны через:

```text
GET /api/metrics
```

Метрики содержат:

* `successfulTransitions` - количество успешных переходов;
* `failedTransitions` - количество ошибочных переходов;
* `duplicateDeliveries` - количество повторных доставок;
* `compensations` - количество компенсаций;
* `averageLatencyMsByStep` - грубая средняя задержка по шагам.

## Health checks

Проверка живости:

```text
GET /health/live
```

Она возвращает `200 OK`, если процесс API запущен.

Проверка готовности:

```text
GET /health/ready
```

Обычно она возвращает `200 OK`. После двух критических сбоев шага `PrepareEquipment` readiness возвращает `503 Service Unavailable`. Это показывает, что сервис жив, но находится в деградированном состоянии и не должен получать новый трафик.

Для учебного восстановления добавлен endpoint:

```text
POST /api/admin/recover
```

Он сбрасывает критическую деградацию и снова делает readiness успешным.

## Основные endpoints

Создать или продвинуть процесс событием:

```text
POST /api/bookings/events
```

Пример тела:

```json
{
  "processKey": "booking-1",
  "idempotencyKey": "event-1",
  "eventName": "ReserveRoom",
  "correlationId": "corr-001",
  "failStep": false
}
```

Получить состояние процесса:

```text
GET /api/bookings/{processKey}
```

Получить журнал:

```text
GET /api/logs
```

Получить метрики:

```text
GET /api/metrics
```

## Как запустить

Откройте PowerShell в папке проекта:

```powershell
cd C:\progects\Frameworks\task_4
```

Задайте локальные папки для .NET и NuGet:

```powershell
$env:DOTNET_CLI_HOME='C:\progects\Frameworks\task_4\.dotnet'
$env:APPDATA='C:\progects\Frameworks\task_4\.appdata'
$env:NUGET_PACKAGES='C:\progects\Frameworks\task_4\.nuget\packages'
```

Соберите проект:

```powershell
dotnet build Task4Framework.sln --configfile NuGet.Config -m:1
```

Ключ `-m:1` нужен для этой учебной среды, чтобы сборка шла последовательно и не упиралась в ограничения параллельного запуска компилятора.

Запустите API:

```powershell
dotnet run --project src\MeetingBooking.Api\MeetingBooking.Api.csproj --urls http://127.0.0.1:5400
```

После запуска сервис доступен по адресу:

```text
http://127.0.0.1:5400
```

## Как проверить вручную

Проверка живости:

```powershell
Invoke-RestMethod http://127.0.0.1:5400/health/live
```

Успешный процесс:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-1","idempotencyKey":"k1","eventName":"ReserveRoom","correlationId":"c1"}'
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-1","idempotencyKey":"k2","eventName":"NotifyParticipants","correlationId":"c2"}'
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-1","idempotencyKey":"k3","eventName":"PrepareEquipment","correlationId":"c3"}'
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-1","idempotencyKey":"k4","eventName":"ConfirmBooking","correlationId":"c4"}'
```

Проверка повторной доставки:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-2","idempotencyKey":"same-key","eventName":"ReserveRoom","correlationId":"repeat-1"}'
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-2","idempotencyKey":"same-key","eventName":"ReserveRoom","correlationId":"repeat-2"}'
```

Во втором ответе будет `duplicate: true`, а состояние останется `RoomReserved`.

Проверка компенсации:

```powershell
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-3","idempotencyKey":"k1","eventName":"ReserveRoom","correlationId":"fail-1"}'
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-3","idempotencyKey":"k2","eventName":"NotifyParticipants","correlationId":"fail-2"}'
Invoke-RestMethod -Uri http://127.0.0.1:5400/api/bookings/events -Method Post -ContentType 'application/json' -Body '{"processKey":"booking-3","idempotencyKey":"k3","eventName":"PrepareEquipment","correlationId":"fail-3","failStep":true}'
```

Ответ покажет `compensated: true`, а состояние вернется в `RoomReserved`.

Посмотреть журнал и метрики:

```powershell
Invoke-RestMethod http://127.0.0.1:5400/api/logs
Invoke-RestMethod http://127.0.0.1:5400/api/metrics
```

## Автоматические проверки

Из папки проекта выполните:

```powershell
$env:DOTNET_CLI_HOME='C:\progects\Frameworks\task_4\.dotnet'
$env:APPDATA='C:\progects\Frameworks\task_4\.appdata'
$env:NUGET_PACKAGES='C:\progects\Frameworks\task_4\.nuget\packages'
dotnet run --no-restore --project tests\MeetingBooking.Tests\MeetingBooking.Tests.csproj
```

Ожидаемый результат:

```text
PASS state machine completes booking
PASS duplicate delivery is idempotent
PASS failed step runs compensation
PASS readiness fails after critical degradation
PASS logs and metrics are observable
```

## Экспериментальная часть

В эксперименте менялись входные события:

* обычная последовательность из четырех шагов;
* повторная доставка события с тем же `idempotencyKey`;
* сбой шага `PrepareEquipment`;
* два критических сбоя подряд для проверки readiness;
* просмотр журналов и метрик после обработки событий.

Наблюдались такие результаты:

* успешная цепочка завершает процесс состоянием `Completed`;
* повторная доставка не меняет состояние и отмечается как `duplicate`;
* сбой подготовки оборудования запускает компенсацию и возвращает процесс в `RoomReserved`;
* после критической деградации `/health/ready` возвращает `503`;
* в журнале видны `correlationId`, переходы, повторы и компенсации;
* в метриках растут счетчики успешных переходов, ошибок, повторов и компенсаций.

Автоматическая проверка подтвердила эти результаты.

## Риски и ограничения

Данные хранятся в памяти процесса. После перезапуска API процессы, журналы и метрики очищаются. Для учебной работы это нормально, но для реальной системы нужно использовать базу данных или журнал событий.

Идемпотентность тоже хранится в памяти. В реальной распределенной системе ключи идемпотентности нужно хранить во внешнем надежном хранилище, иначе при перезапуске сервис забудет уже обработанные события.

Метрика задержки сделана грубо: считается среднее время выполнения шага внутри приложения. Для промышленной системы лучше использовать Prometheus/OpenTelemetry и отдельные гистограммы.

Компенсация реализована для одного учебного сценария. Если процесс станет сложнее, компенсации лучше выделить в отдельные обработчики шагов.

## Выводы

В работе реализована учебная веб-служба с машиной состояний для бронирования переговорной. Сервис выдерживает повторную доставку событий через ключ идемпотентности, выполняет компенсацию при сбое шага, пишет наблюдаемые журналы с `correlationId`, отдает health checks и показывает основные метрики. Автоматические проверки подтверждают основные требования задания.
