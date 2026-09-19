# Arkitektur – ALISflyt

Dette dokumentet beskriver kort hvordan koden er organisert og hvilket ansvar de ulike lagene har.

> Web viser. Application koordinerer. Domain bestemmer. Infrastructure lagrer. Integrations kommuniserer.

## Lagene i løsningen

### Alisflyt.Domain

Inneholder forretningsbegreper, entiteter, statuser og regler. Domain skal ikke kjenne Web, database eller eksterne systemer.

Eksempler:

- `Alisflyt.Domain/Entities/GrantCase.cs`
- `Alisflyt.Domain/Enums/GrantCaseStatus.cs`

### Alisflyt.Application

Koordinerer brukstilfellene og definerer tjenester, DTO-er, request-modeller og tekniske kontrakter.

Eksempel:

- `Alisflyt.Application/Services/IGrantCaseApplicationService.cs`

Application bruker Domain, men skal ikke avhenge av Infrastructure.

### Alisflyt.Infrastructure

Implementerer tekniske løsninger for database og lagring. Her ligger EF Core, databasekontekst, repositories, migrasjoner, saksnummergenerator og utviklingsdata.

Eksempler:

- `Alisflyt.Infrastructure/Persistence/ApplicationDbContext.cs`
- `Alisflyt.Infrastructure/Persistence/Repositories/GrantCaseRepository.cs`
- `Alisflyt.Infrastructure/Persistence/Migrations/`

Infrastructure avhenger av Application og Domain og implementerer kontrakter definert i Application.

### Alisflyt.Web

Inneholder ASP.NET Core MVC-controllerne, ViewModels, Razor Views, navigasjon og statiske filer.

Eksempler:

- `Alisflyt.Web/Controllers/GrantCasesController.cs`
- `Alisflyt.Web/Views/GrantCases/Details.cshtml`

`Alisflyt.Web/Program.cs` starter applikasjonen og fungerer som composition root. Her kobles Application-kontraktene til implementasjonene i Infrastructure.

### Alisflyt.Integrations

Er klargjort for fremtidig kommunikasjon med eksterne systemer, for eksempel P360, Helsedirektoratet og e-posttjenester. Integrasjonene er foreløpig ikke implementert.

### Testprosjektene

Testene ligger under `tests/`:

- `Alisflyt.Domain.Tests` tester forretningsregler.
- `Alisflyt.Application.Tests` tester brukstilfeller og mapping.
- `Alisflyt.Web.Tests` tester controlleroppførsel.
- `Alisflyt.IntegrationTests` tester Infrastructure mot LocalDB.

## Avhengighetsretning

- Domain har ingen avhengighet til de øvrige prosjektlagene.
- Application avhenger av Domain.
- Infrastructure avhenger av Application og Domain.
- Web bruker Application og Infrastructure og kobler løsningen sammen i `Program.cs`.
- Application skal ikke avhenge av Infrastructure.

## Hvor skal nye filer ligge?

| Behov | Plassering |
|---|---|
| Forretningsregel, entitet eller status | `Alisflyt.Domain` |
| Brukstilfelle, DTO, request eller kontrakt | `Alisflyt.Application` |
| Database, repository eller migrasjon | `Alisflyt.Infrastructure` |
| Controller, ViewModel eller Razor View | `Alisflyt.Web` |
| Ekstern systemintegrasjon | `Alisflyt.Integrations` |
| Automatisert test | Tilsvarende prosjekt under `tests/` |

## Eksempel: Fra controller til database

1. Brukeren sender et skjema til `GrantCasesController.Create`.
2. Controlleren mapper ViewModel til `CreateGrantCaseRequest` og kaller `IGrantCaseApplicationService.CreateDraftAsync`.
3. `GrantCaseApplicationService` koordinerer brukstilfellet.
4. Domain oppretter `GrantCase` og håndhever forretningsreglene.
5. Application bruker `IGrantCaseRepository` for å be om lagring.
6. `GrantCaseRepository` og `ApplicationDbContext` i Infrastructure lagrer saken med EF Core.
7. Application returnerer en DTO, som Web mapper til en ViewModel.

## Implementert

- Opprette og lagre ufullstendige utkast
- Hente og vise saker
- Redigere utkast
- Validere og sende inn søknader
- Rolleportal og ALIS-dashboard
- Skrivebeskyttet koordinatorvisning
- Presentasjon av planlagt veilederområde
- EF Core-persistens, migrasjoner og demodata
- Automatiserte tester og GitHub Actions

## Planlagt

- Autentisering og autorisasjon
- Eierskap og avgrensning av saker per bruker
- Veiledningstimer og avstemming
- Koordinatorbehandling og retur for korrigering
- Sakshistorikk
- Dokumentopplasting og beregninger
- Revisorgodkjenning
- Frister og varsling
- Integrasjoner mot P360 og Helsedirektoratet

Se `Alisflyt.Web/Program.cs` for hvordan applikasjonen settes sammen, og `README.md` for oppstart og lokal utvikling.
