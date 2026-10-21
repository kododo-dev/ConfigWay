# Flagi funkcji w ConfigWay: plan

Stan: F0-F2 i F3 (bez wydania) zrobione (2026-10-21); zostało wydanie 2.1.0. Źródło pomysłu: `IDEAS.md`, pkt 2.

## 1. Cel i zakres

Nowa, opcjonalna paczka **`Kododo.ConfigWay.FeatureManagement`**. Pozwala zarządzać flagami
`Microsoft.FeatureManagement` z panelu ConfigWay bez redeployu. Flagi dalej czyta standardowy
`IFeatureManager` / `IVariantFeatureManager` z `IConfiguration`, więc kod aplikacji
(`[FeatureGate]`, `IsEnabledAsync`, tag helpery) się nie zmienia.

Zależność od `Microsoft.FeatureManagement` (4.8.0: net8.0 + net10.0; net9.0 korzysta z buildu net8.0)
ma tylko nowa paczka. Rdzeń ConfigWay, UI i magazyny jej nie ciągną.

**v1:**
- lista flag ze wszystkich źródeł (appsettings, env, ConfigWay) z oznaczeniem źródła i nadpisania;
- szybkie włącz/wyłącz z listy;
- edytor flagi: `enabled`, `requirement_type` (Any/All), filtry `Microsoft.Percentage`,
  `Microsoft.TimeWindow` (bez cykliczności), `Microsoft.Targeting` (użytkownicy, grupy z %, domyślny %,
  wykluczenia) oraz dowolny filtr własny jako nazwa + parametry JSON;
- dodawanie nowych flag i reset nadpisania, czyli powrót do definicji z appsettings albo usunięcie flagi,
  która istnieje tylko w ConfigWay;
- tester "czy flaga jest włączona dla użytkownika X z grupami Y" (ewaluacja po stronie serwera);
- walidacja po stronie serwera; i18n en/pl; demo; testy.

**Później (v1.x):** warianty i alokacja (`variants`, `allocation`, `status_override`), cykliczność
TimeWindow, flaga `telemetry`, narzędzia MCP. Odświeżanie między replikami jest w v1 (sekcja 7).

**Poza zakresem:** historia zmian i audyt (ConfigWay ich nie ma; to osobny temat dla całego ConfigWay),
edycja flag w starym schemacie .NET (`FeatureManagement`). Te flagi są tylko wyświetlane, a nadpisanie
zapisuje się w schemacie Microsoft.

## 2. Stan wyjściowy ConfigWay (istotne fakty)

- Magazyn to płaskie `key -> value` (`IStore`: Get/Set/Delete, tabele `configway.settings`
  w PostgreSQL i SQL Server, `InMemoryStore` domyślnie).
- `ConfigurationProvider.IsAllowedKey` przepuszcza **tylko klucze zarejestrowanych typów `IOptions<T>`**,
  więc wpisy `feature_management:*` byłyby odrzucone.
- `ConfigurationBuilder` ma publiczne tylko `Services`, `Store` i `AddOptions<T>`. Brak punktów
  rozszerzeń dla źródeł konfiguracji i dodatkowych endpointów.
- Przeładowanie: `IConfigurationEditor.ReloadAllAsync` po zapisie, tylko w bieżącej instancji.
- UI: jeden `WayModule("config")`, menu w `SideMenu.tsx`, API Reiho `POST /config/api/{Request}`.
- Way.Shell nie pozwala innej paczce dołożyć widoku do cudzego modułu (jeden `App` na Way).
- ConfigWay nie ma MCP ani testów SPA.

## 3. Model zapisu

**Jedna flaga = jeden wiersz** w istniejącym `IStore`:

```
key:   feature_management:flags/<id>     (prefiks niekolidujący z kluczami IOptions)
value: JSON flagi w schemacie Microsoft  {"id":"Beta","enabled":true,"conditions":{...}}
```

Dlaczego tak, a nie spłaszczone `feature_management:feature_flags:0:...`:
- indeksy tablicy rozjeżdżają się przy dodawaniu i usuwaniu, a scalanie po indeksie z appsettings
  nadpisywałoby niewłaściwe flagi;
- zapis jednej flagi jest atomowy (jeden upsert) i nie wymaga zmian schematu baz;
- reset flagi to usunięcie jednego klucza.

Nie trzeba zmieniać tabel ani `IStore`.

**Źródło konfiguracji paczki:** `FeatureFlagsProvider : StoreConfigurationProvider` wybiera z magazynu klucze
z prefiksem `feature_management:flags/`, parsuje JSON i wystawia go spłaszczony jako
`feature_management:feature_flags:{100000+i}:...`. Główny provider ConfigWay dalej ignoruje te klucze, bo nie
należą do żadnego typu opcji.

**Scalanie z appsettings po `id`:** paczka włącza
`ConfigurationFeatureDefinitionProviderOptions.CustomConfigurationMergingEnabled = true`
(od 4.3.0). Wtedy definicja z ConfigWay (źródło dodane później) wygrywa w całości z flagą o tym samym
`id` z appsettings, niezależnie od indeksów.

**Sprawdzone w F1 (źródła `ConfigurationFeatureDefinitionProvider` 4.8.0 + testy):**
- Przy scalaniu każdy provider (`ConfigurationProvider` albo `ChainedConfigurationProvider`) jest osobnym
  źródłem, a wygrywa ten o najwyższym priorytecie, czyli dodany później. W obrębie źródła wpis w schemacie
  Microsoft wygrywa ze starym schematem, więc nadpisanie flagi ze starego schematu działa.
- Jeśli aplikacja ma providera, który nie dziedziczy z `ConfigurationProvider`, biblioteka wraca do
  zwykłego scalania po indeksach. Dlatego flagi z ConfigWay zaczynają się od indeksu **100000**: są wtedy
  po flagach aplikacji, a biblioteka bierze ostatnią definicję o danym `id`. Mutacja w testach: każde
  z dwóch zabezpieczeń osobno wystarcza, bez obu polegają 4 testy nadpisań.
- Id porównywane bez wielkości liter; brak `enabled` = wyłączona.

## 4. Zmiany w rdzeniu ConfigWay (F0), zrobione 2026-10-20

Publiczne punkty rozszerzeń, bez zmian zachowania dla obecnych użytkowników:

1. **`StoreConfigurationProvider`** (Kododo.ConfigWay): abstrakcyjna klasa bazowa providera, który
   wystawia wartości z magazynu pod własnymi kluczami (`Select(IReadOnlyList<Setting>)`). Rejestracja
   przez `ConfigurationBuilder.AddProvider(provider)`. Główny provider ConfigWay też z niej dziedziczy
   i czyta magazyn **raz** dla wszystkich: przy `Load`, po zapisie (`IConfigurationEditor.ReloadAllAsync`)
   i przy odpytywaniu. Providery rozszerzeń są dodawane do `IConfiguration` zaraz po głównym.
   `GetConfigurationHandler` wyłącza wszystkie `StoreConfigurationProvider` z wartości domyślnych.
2. **`AddUiExtension(id, assembly)`** (Kododo.ConfigWay.UI): rejestruje handlery Reiho z assembly
   i rozszerzenie w DI. `UseConfigWay` mapuje requesty rozszerzeń na tę samą grupę `/api`, więc
   dziedziczą autoryzację grupy. Użytkownik nie wywołuje nic dodatkowego.
3. **`GetCapabilities`** -> `{ extensions: string[] }`. SPA na tej podstawie pokaże "Flagi" (F2).
4. **`ReloadInterval`** (sekcja 7).
5. Way.Shell podbity do 2.1.1 (NuGet). Paczka npm zostaje na 2.0.0 do F2.

Testy: 192 w ConfigWay.Tests (net8/9/10), po 24 w PostgreSQL i SQL Server (w tym test dwóch replik).

## 5. API publiczne paczki

```csharp
builder.AddConfigWay(x =>
{
    x.UsePostgreSql(cs);
    x.AddOptions<SmtpOptions>();
    x.AddUiEditor();
    x.AddFeatureFlags();            // źródło + handlery + rozszerzenie UI
});

builder.Services.AddFeatureManagement().WithTargeting<MyTargetingContextAccessor>();

app.UseConfigWay().RequireAuthorization("admin"); // bez zmian; flagi pod /config
```

`AddFeatureFlags` **nie** wywołuje `AddFeatureManagement`. Aplikacja sama decyduje o targetingu
i własnych filtrach. Paczka tylko konfiguruje opcje scalania i rejestruje swoje elementy. Opcjonalnie
`AddFeatureFlags(o => o.KnownFilters...)` podpowiada nazwy własnych filtrów w UI.

## 6. Backend (Reiho, w paczce FeatureManagement), zrobione 2026-10-21

Requesty (pod `/config/api`, z autoryzacją grupy ConfigWay):

| Request | Działanie |
|---|---|
| `GetFeatureFlags` -> `{ isFeatureManagementRegistered, filters[], flags[] }` | Pozycja: `id`, `flag` (w użyciu), `configured` (z konfiguracji aplikacji albo `null`), `hasOverride`, `isDotnetSchema`, `hasVariants`, `error` (nieczytelne nadpisanie). `filters` to aliasy zarejestrowanych `IFeatureFilterMetadata` (podpowiedzi w UI). Flagi bazowe czyta `FlagCatalog` z każdego providera osobno (bez `Load`), tak jak scala je biblioteka. |
| `SaveFeatureFlag(flag, isNew)` | Walidacja; `isNew` + istniejące id -> 400 `flag.id`; zmiana nieistniejącej -> 404. Zachowuje id istniejącej flagi (wielkość liter) i jej nieedytowane pola. Upsert jednego klucza + przeładowanie. |
| `ResetFeatureFlag(id)` | Usuwa nadpisanie (404, gdy go nie ma). Flaga z konfiguracji wraca, flaga tylko z ConfigWay znika. |
| `EvaluateFeatureFlag(id, userId?, groups?)` -> `{ enabled, variant }` | `IsEnabledAsync(id, TargetingContext)` + `GetVariantAsync`; brak `AddFeatureManagement` -> 409, nieznana flaga -> 404. Informacji "który filtr zdecydował" biblioteka nie udostępnia, więc jej nie ma. |

Model edycji (`FeatureFlag`): `id`, `enabled`, `requirementType` (Any/All), `filters[]` (`name`,
`parameters` jako JSON), `description`.

**Walidacja** (`IValidatableObject`; Reiho zwraca ProblemDetails z kluczami typu
`flag.filters[1].parameters`):
- `id`: niepuste, bez spacji na brzegach, bez `:`, najwyżej 200 znaków;
- Percentage: `Value` w zakresie 0-100;
- TimeWindow: `Start` i/lub `End` jako data, `Start < End`; zapis w RFC 1123 (UTC); `Recurrence`
  zachowywane bez sprawdzania (edycja w F4);
- Targeting: `Audience` wymagane, niepuste nazwy, grupy bez duplikatów, procenty 0-100;
- inne filtry: dowolne parametry, ale **nazwa musi należeć do zarejestrowanego filtra**, gdy aplikacja ma
  `AddFeatureManagement` (`RegisteredFilters`, dopasowanie jak w bibliotece: pełny alias albo ostatni segment,
  `AlwaysOn`/`On` zawsze). Powód: dla nieznanego filtra biblioteka rzuca `FeatureManagementException` przy
  każdym sprawdzeniu flagi; sprawdzone na demo (strona 500). Błąd: `flag.filters[i].name`.

**Zapis:** JSON w schemacie Microsoft, parametry wbudowanych filtrów w jednym kształcie (liczby jako liczby).
Nieedytowane pola (`variants`, `allocation`, `telemetry`, ...) są **zachowywane**, także przy pierwszym
nadpisaniu flagi z appsettings. Warianty flag ze starego schematu nie są przenoszone (`HasDroppedVariants`
w `BaseFlag`; UI może to pokazać).

**Testy:** `ConfigWay.FeatureManagement.Tests` (82 × net8/9/10), w tym integracja z prawdziwym
`IVariantFeatureManager`: nadpisanie, reset, nowa flaga, stary schemat, Percentage, TimeWindow, Targeting
przez `EvaluateFeatureFlag`, warianty zachowane po zapisie, wielkość liter id, nieczytelne nadpisanie.
PostgreSQL i SQL Server: trwałość po restarcie i przekazanie zmiany do drugiej repliki (`ReloadInterval`).
CI (`ci.yml`) uruchamia nowe testy, a `publish-nuget.yml` pakuje nową paczkę.

## 7. Wiele instancji, w v1 (decyzja 2026-10-20), zrobione w F0

Bez tej opcji zmiana działa tylko w instancji, która ją zapisała. Przy flagach to szczególnie boli,
bo wyłącznik awaryjny musi zadziałać wszędzie. Opcja `x.ReloadInterval = TimeSpan.FromSeconds(30)`
(domyślnie `null`, czyli wyłączona) uruchamia `ReloadService` (`BackgroundService` + `PeriodicTimer`).
Ten co interwał czyta magazyn i przeładowuje wszystkie providery tylko wtedy, gdy zmienił się jakikolwiek
wpis (porównanie z ostatnio wczytanym stanem). Błąd magazynu jest logowany jako ostrzeżenie, a kolejny
tick próbuje ponownie. Bez zmian `IStore`. Tabele są małe, więc koszt jest pomijalny; ewentualnie
później tani znacznik wersji w magazynach.

## 8. UI (w ConfigWay.UI SPA), zrobione 2026-10-21

Widok flag mieszka w **istniejącym SPA ConfigWay** i pojawia się, gdy `GetCapabilities` zwraca `flags`
(`FlagsProvider` pobiera flagi tylko wtedy). Uzasadnienie: Way.Shell nie ma slotów na widoki z innych
paczek, a osobny Way byłby w menu tylko w panelu `/admin`. ConfigWay.UI nie ma zależności NuGet od paczki
flag; `dist/way.js` urósł do ~96 kB (24 kB gzip). Way.Shell npm podbity do ^2.1.1.

- **Trasy:** `flags` (lista), `flags/new`, `flag/:flagId`. Dopóki `GetCapabilities` nie odpowie, nieznana
  trasa nie przekierowuje do korzenia (głęboki link do flagi działa).
- **Lista** (`FlagsPage`): przełącznik zapisuje od razu; snackbar z "Cofnij" (gdy flaga nie miała
  nadpisania, cofnięcie robi reset, a nie zapis kopii). Opis warunków ("1 user · 1 group", "50% of checks",
  "from ... until ..."), znaczniki: nadpisana / tylko ConfigWay / schemat .NET / warianty / nieczytelna.
  Wyszukiwarka po id i opisie, ostrzeżenie, gdy aplikacja nie wywołała `AddFeatureManagement`.
- **Edytor** (`FlagPage`): id (tylko dla nowej), opis, `enabled`, źródło flagi; "Przywróć z konfiguracji"
  albo "Usuń flagę" z oknem potwierdzenia. Warunki: menu "Dodaj warunek", Any/All przy 2+ warunkach,
  karty filtrów z błędami API przypisanymi po `flag.filters[i]`:
  - Percentage: suwak + pole; podpowiedź, że to losowanie na sprawdzenie (nie na użytkownika);
  - TimeWindow: `datetime-local` w strefie przeglądarki, zapis w UTC, stan "aktywne / jeszcze nie / zakończone";
    `Recurrence` zachowane z informacją;
  - Targeting: chipy użytkowników i wykluczeń, wiersze grup z %, % pozostałych;
  - własny filtr: nazwa z podpowiedziami zarejestrowanych filtrów, ostrzeżenie dla niezarejestrowanej
    (sprawdzenie flagi rzuciłoby wyjątek), parametry JSON z blokadą zapisu przy błędnym JSON.
- **Tester** (`FlagCheck`): użytkownik + grupy -> włączona/wyłączona i wariant; sprawdza zapisany stan.
- **i18n:** en/pl w `translations.ts` (funkcje dla tekstów z liczbami).
- `PageHeader` dostał `actions` i `searchPlaceholder`.

**Demo:** klasa opcji `FeatureFlags` zastąpiona prawdziwymi flagami w `appsettings.json` (BetaBanner,
NewCheckout z targetingiem, HolidayTheme z oknem czasowym, MaintenanceMode, SearchEngine z wariantami,
ReadOnlyMode w schemacie .NET). Strona główna pokazuje stan flag dla gościa wybranego linkiem
(`?user=alice|bob|mallory`).

**e2e szczegółowe** (`ConfigWay.Demo.Web/e2e/flags.mjs`, 53 scenariusze): linki bezpośrednie, wyszukiwanie,
okno czasowe tam i z powrotem przez strefę (wartości w bazie w UTC), edycja targetowania (chipy, grupy,
wykluczenia) z testerem i stroną demo, Any/All na dwóch warunkach, własny filtr (zły JSON, niezarejestrowana
nazwa), id `Team.Billing/V2`, duplikat id, `:` w id, Odrzuć, stary schemat .NET, warianty zachowane po zapisie,
zapis ustawień nie gubi nadpisań flag, telefon (bez poziomego przewijania), język polski. 53/53 w Chrome,
Edge, Firefox i WebKit. Bezpieczeństwo w testach integracyjnych: 403 bez decyzji, 401 bez zalogowania
(wszystkie 4 requesty, flaga nietknięta). Tester czyści wynik po zmianie użytkownika lub grup.

**e2e** (`ConfigWay.Demo.Web/e2e/check.mjs`): +18 scenariuszy flag (menu, lista, przełącznik i cofnięcie,
targeting i tester, nadpisanie i reset, błąd walidacji w karcie warunku, nowa flaga i usunięcie). Skrypt
najpierw resetuje nadpisania, których dotyka. Wynik: 32/32 w Chrome i WebKit. `SHOTS=<dir>` zapisuje zrzuty.

## 9. Testy

- `ConfigWay.FeatureManagement.Tests` (net8/9/10):
  - parser i spłaszczanie JSON ↔ klucze;
  - walidacja;
  - handlery;
  - **integracja z prawdziwym `IFeatureManager`**: flaga z appsettings, nadpisanie z ConfigWay, reset,
    Percentage, TimeWindow, Targeting przez `EvaluateFeatureFlag`; zachowanie nieznanych pól.
- PostgreSQL/SQL Server: po jednym teście API na Testcontainers (zapis, odczyt, przeładowanie).
- Testy rdzenia po F0: brak regresji (istniejące 170+ testów) i nowe testy punktów rozszerzeń.
- e2e: nowe scenariusze w `ConfigWay.Demo.Web/e2e/check.mjs` (lista, przełącznik, edytor, reset, tester).

## 10. Demo i dokumentacja

- Demo: klasę `FeatureFlags` (4 boole jako zastępstwo flag) zastępujemy prawdziwymi flagami
  w `appsettings.json` (np. `NewCheckout` z Percentage, `BetaBanner` z Targeting,
  `MaintenanceMode`, `HolidayTheme` z TimeWindow). `HomeView` pokazuje ich stan dla bieżącego
  (wybieralnego) użytkownika.
- README: tabela paczek, sekcja "Feature flags", `.nuget/README.md` nowej paczki,
  aktualizacja Limitations.
- Zrzut ekranu widoku flag w `docs/screenshots`.

## 11. Fazy

| Faza | Zakres | Efekt |
|---|---|---|
| F0 ✅ | Punkty rozszerzeń w rdzeniu i UI (sekcja 4), `ReloadInterval` + testy | Bez zmian zachowania |
| F1 ✅ | Backend paczki: źródło, scalanie, handlery, walidacja, testy integracyjne z `IFeatureManager` | Flagi edytowalne przez API |
| F2 ✅ | UI: lista, przełącznik, edytor (Percentage/TimeWindow/Targeting/własny), tester, i18n | Pełny v1 w panelu |
| F3 ✅ (bez wydania) | Demo, README, e2e, zrzuty, `publish-nuget.yml` (nowa paczka) | Gotowe do wydania **ConfigWay 2.1.0** |
| F4 (v1.x) | Warianty i alokacja, cykliczność TimeWindow | 2.2.0 |
| później | MCP (lista, przełącz, sprawdź flagę), historia zmian | - |

Wersja: wszystkie paczki ConfigWay wydawane razem z tagu, więc nowa paczka startuje od 2.1.0.
Way.Shell bez zmian (kontrakt 2).
