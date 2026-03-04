# Журнал изменений (CHANGELOG)

## Что делаем

Доработка и стабилизация приложения **Хранитель** — исправление сборки, поведения при закрытии, работы иконки в трее и одиночного экземпляра.

## Преодолённые проблемы

1. **CS0579 — дублирование атрибутов сборки**  
   WPF временный проект `wpftmp` собирал те же атрибуты. Решение: `GenerateAssemblyInfo=false` в `Directory.Build.props`, ручной `AssemblyInfo.cs`, исключение `GenIcon\**` из компиляции Hranitel.

2. **TaskCanceledException в ProcessMonitorService.Stop()**  
   После `Cancel()` вызов `Task.Wait()` приводил к `AggregateException`. Решение: обёртка в try/catch с `Handle` для `OperationCanceledException` и `TaskCanceledException`.

3. **Клик по иконке в трее не работал**  
   Добавлен обработчик `Click` наравне с `DoubleClick` — оба показывают окно.

4. **Клик по ярлыку при уже запущенном приложении**  
   Второй экземпляр сигнализирует первому через `EventWaitHandle`, первый показывает окно.

5. **Приложение не запускалось, не было окна и иконки в трее**  
   `GenIcon\Program.cs` содержал top-level statements и попадал в сборку Hranitel — компилятор выбирал его как точку входа. Решение: `<Compile Remove="GenIcon\**" />` в csproj.

6. **ApplicationException при закрытии: "Object synchronization method was called from an unsynchronized block of code"**  
   `ReleaseMutex()` в `App.OnExit` вызывался, когда поток не владел мьютексом. Решение: обёртка вызова `ReleaseMutex()` в try/catch для `ApplicationException`.

7. **Блокировка не работала — приложения не завершались**  
   В `GetBlockedAppsToKill` все процессы помещались в `Dispose()` в `finally`, включая те, что добавлялись в результат. В итоге `Kill()` вызывался для уже disposed процессов и молча не срабатывал. Решение: не вызывать `Dispose()` для процессов из результата.
