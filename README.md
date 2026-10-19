# AlfaHackaton.HttpClient

Типизированный HTTP-клиент для .NET 10: JSON-запросы, заголовки, отмена и чтение JSON-массивов потоком.
Использует `IHttpClientFactory` и `Newtonsoft.Json`.
Контекст запроса и результаты операций подключаются из `AlfaHackaton.Context` и `AlfaHackaton.Result`.

## Подключение

Добавьте ссылку на проект из каталога приложения:

```powershell
dotnet add YourApp.csproj reference ../AlfaHackaton.HttpClient/AlfaHackaton.HttpClient.csproj
```

Для восстановления пакетов используйте источники из [NuGet.config](NuGet.config).
Доступ к GitHub Packages задаётся переменной окружения:

```powershell
$env:NuGetPackageSourceCredentials_github = "Username=LOGIN;Password=TOKEN;ValidAuthenticationTypes=Basic"
```

Токену нужен доступ `read:packages`.

## Регистрация

```csharp
using AlfaHackaton.HttpClient;
using AlfaHackaton.HttpClient.Common.Settings;

builder.Services.AddAlfaHttpClient(new CatalogHttpClientSettings
{
    BaseAddress = "https://example.com/api/",
    Timeout = TimeSpan.FromSeconds(30)
});

public class CatalogHttpClientSettings : HttpClientSettings;
```

Для каждого внешнего API используйте свой тип настроек. Метод возвращает `IHttpClientBuilder`:
можно добавить обработчики через `AddHttpMessageHandler` и настроить время жизни через `SetHandlerLifetime`.

## Запросы

Получите `IHttpClient<CatalogHttpClientSettings>` через DI:

```csharp
using AlfaHackaton.Context;
using AlfaHackaton.HttpClient;
using AlfaHackaton.HttpClient.Common.Interfaces;
using AlfaHackaton.Result;
using Microsoft.Extensions.Logging;

public class CatalogService(
    IHttpClient<CatalogHttpClientSettings> client,
    ILogger<CatalogService> logger)
{
    public async Task<Result<CatalogItem>> GetItemAsync(Context ctx, int id)
    {
        var response = await client.GetContext<CatalogItem>(ctx, $"items/{id}");
        if (!await response.IsStatusOkAsync("Не удалось получить товар", logger))
        {
            return response.CreateErrorResult<CatalogItem>("Не удалось получить товар");
        }

        return response.Result;
    }
}

public class CatalogItem
{
    public string Name { get; set; } = string.Empty;
}
```

Отправка JSON и передача токена:

```csharp
using System.Net.Http.Headers;

var response = await client.Post<CatalogItem, CatalogItem>(
    "items",
    new CatalogItem { Name = "Пример" },
    (headers, _) => headers.Authorization = new AuthenticationHeaderValue("Bearer", token),
    cancellationToken);
```

Также доступны `Put`, `Patch`, `Delete` и универсальный `Send`. В `Data` можно передать `HttpContent`
для формы или файла, в `Request.RawData` — массив байтов.

Методы `GetContext`, `PostContext`, `PutContext`, `PatchContext` и `DeleteContext` берут токен отмены
и заголовки из `Context`. `AuthToken` содержит готовое значение Authorization, например `Bearer ...`.
Передаются Origin, User-Agent, Locale-Id и AdditionalHeaders; явные AdditionalHeaders
имеют приоритет. Без токена остальные заголовки тоже передаются.

`IsStatusOkAsync` проверяет ровно `200 OK` и логирует остальные статусы.
`CreateError` и `CreateErrorResult<T>` сохраняют HTTP-статус; источник ошибки по умолчанию — External.

`IsSuccessStatusCode` означает статус 200–299. Ответы 4xx/5xx возвращаются как `Response`:
код доступен в `Status`, тело — в `TextData` и `RawData`. `Result` десериализует JSON в указанный тип
и кэширует результат. Сетевые ошибки, таймауты и отмена передаются исключениями .NET.

## Чтение JSON-массива потоком

```csharp
using AlfaHackaton.HttpClient.Common.Models;

await foreach (var item in client.GetAsAsyncEnumerable<None, CatalogItem>(
    "items", null, cancellationToken: cancellationToken))
{
    Console.WriteLine(item?.Name);
}
```

Метод читает JSON-массив по элементам; SSE и NDJSON не поддерживаются.

## Проверка

```powershell
dotnet test AlfaHackaton.HttpClient.slnx
```

Сборка NuGet-пакета:

```powershell
dotnet pack AlfaHackaton.HttpClient.csproj --configuration Release --output artifacts/nupkg
```

При push в `master` GitHub Actions запускает тесты и публикует пакет в GitHub Packages.
Для публикации нужен secret `GH_TOKEN`.
