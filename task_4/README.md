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

## Использованные паттерны

В проекте использованы несколько простых паттернов и архитектурных приемов.

`State Machine` - основной паттерн этой работы. Процесс бронирования представлен как набор состояний и разрешенных переходов. В коде это видно в `BookingStore.Move`: событие применяется только тогда, когда оно подходит к текущему состоянию. Такой подход удобен для объяснения процесса, потому что можно явно показать, где находится бронирование и какие события допустимы дальше.

`Idempotent Consumer` - обработчик события не должен выполнять одно и то же действие два раза, если событие пришло повторно. Для этого внутри процесса хранится словарь уже обработанных `idempotencyKey`. Если ключ уже есть, состояние не меняется, а сервис возвращает сохраненный результат с признаком `duplicate`.

`Saga` с компенсацией - процесс состоит из нескольких шагов, и при сбое следующего шага нужно откатить результат предыдущего. В этой работе это сделано для сбоя `PrepareEquipment`: если оборудование подготовить не удалось, уведомление участников считается отмененным, а состояние возвращается в `RoomReserved`.

`Repository / In-memory Store` - состояние процессов, журнал и метрики вынесены в отдельные классы-хранилища: `BookingStore`, `ProcessLogStore`, `MetricsStore`. Это не полноценная база данных, но структура похожа на репозиторий: маршруты API не работают напрямую со словарями и очередями.

`Middleware` - единая обработка ошибок вынесена в `ErrorMiddleware`. Благодаря этому endpoints не дублируют `try/catch`, а ошибки возвращаются в одном JSON-формате.

`Dependency Injection` - сервисы регистрируются в `Program.cs` через `AddSingleton`. Это позволяет передавать хранилища и состояние readiness в обработчики запросов без ручного создания объектов.

`Health Check` - реализованы отдельные проверки живости и готовности: `/health/live` и `/health/ready`. Это стандартный подход для веб-служб, чтобы отличать "приложение запущено" от "приложение готово принимать трафик".

`Observer / Monitoring` в простом виде - журнал и метрики позволяют наблюдать за работой сервиса. По ним видно, какие переходы были выполнены, какие события пришли повторно, где была компенсация и сколько было ошибок.

## Паттерны для расширения системы

Если систему расширять, текущей простой реализации будет мало. Тогда понадобятся более серьезные паттерны.

`Outbox Pattern` - нужен, если сервис будет сохранять состояние в базу данных и отправлять события во внешний брокер. Outbox помогает не потерять событие: сначала изменение и событие сохраняются в одной транзакции, потом отдельный процесс отправляет событие наружу.

`Inbox Pattern` - развитие текущей идемпотентности. Сейчас обработанные ключи хранятся в памяти, а в реальной системе их нужно хранить в базе. Inbox позволит помнить уже обработанные сообщения даже после перезапуска сервиса.

`Persistent Saga` - текущая saga хранится только в памяти. При расширении шаги процесса, компенсации и текущий статус нужно сохранять в базе данных, чтобы процесс можно было продолжить после перезапуска.

`Strategy` - подойдет для разных правил бронирования. Например, для обычной переговорки, большой аудитории и VIP-комнаты могут быть разные проверки доступности, разные ограничения и разные компенсации. Тогда правила можно вынести в отдельные стратегии.

`Command Handler` - сейчас все события идут через один метод `Apply`. При росте системы лучше сделать отдельные обработчики команд: `ReserveRoomHandler`, `NotifyParticipantsHandler`, `PrepareEquipmentHandler`. Так код будет легче тестировать и расширять.

`Unit of Work` - понадобится при переходе на базу данных. Он позволит сохранять изменение состояния процесса, запись идемпотентности, журнал и метрики согласованно, а не отдельными несвязанными операциями.

`Circuit Breaker` - нужен, если шаги процесса начнут обращаться к внешним сервисам, например к календарю, почте или сервису оборудования. Если внешний сервис часто падает, circuit breaker временно остановит обращения к нему и защитит систему от лишней нагрузки.

`Retry with Backoff` - понадобится для временных сбоев. Например, если сервис уведомлений недоступен несколько секунд, можно повторить запрос не сразу, а с увеличивающейся паузой.

`CQRS` - может понадобиться, если появится много запросов на чтение: история бронирований, отчеты, фильтры, аналитика. Тогда команды изменения процесса и запросы чтения можно разделить.

`Event Sourcing` - полезен, если нужно хранить не только текущее состояние, но и полную историю процесса. В этом случае состояние бронирования можно восстанавливать по цепочке событий.

В этой учебной версии все эти паттерны не добавлены специально, потому что они усложнили бы код. Но при росте системы именно они помогут сделать обработку событий надежной, восстановимой и удобной для сопровождения.

## Риски и ограничения

Данные хранятся в памяти процесса. После перезапуска API процессы, журналы и метрики очищаются. Для учебной работы это нормально, но для реальной системы нужно использовать базу данных или журнал событий.

Идемпотентность тоже хранится в памяти. В реальной распределенной системе ключи идемпотентности нужно хранить во внешнем надежном хранилище, иначе при перезапуске сервис забудет уже обработанные события.

Метрика задержки сделана грубо: считается среднее время выполнения шага внутри приложения. Для промышленной системы лучше использовать Prometheus/OpenTelemetry и отдельные гистограммы.

Компенсация реализована для одного учебного сценария. Если процесс станет сложнее, компенсации лучше выделить в отдельные обработчики шагов.

## Выводы

В работе реализована учебная веб-служба с машиной состояний для бронирования переговорной. Сервис выдерживает повторную доставку событий через ключ идемпотентности, выполняет компенсацию при сбое шага, пишет наблюдаемые журналы с `correlationId`, отдает health checks и показывает основные метрики. Автоматические проверки подтверждают основные требования задания.
