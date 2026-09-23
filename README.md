# ALISflyt

ALISflyt er en prototype for digital søknad og behandling av kommunale ALIS-tilskudd. Målet er at informasjon skal registreres én gang og gjenbrukes gjennom prosessen, slik at brukerne får bedre oversikt og koordinator kan bruke mer tid på kontroll og behandling.

Løsningen er under utvikling og skal foreløpig bare brukes med syntetiske testdata.

## Status

### Implementert

- Rolleportal for ALIS, koordinator og veileder
- ALIS-dashboard med oversikt over utkast og innsendte søknader
- Koordinator-dashboard med skrivebeskyttet visning av saker
- Presentasjon av planlagt veilederområde
- Opprettelse og lagring av ufullstendige utkast
- Redigering, validering og innsending av søknader
- Koordinator kan starte behandling av innsendte søknader
- Koordinator kan returnere en sak for korrigering med obligatorisk begrunnelse
- ALIS kan se returbegrunnelse og tidspunkt
- ALIS kan redigere en returnert søknad og sende den inn på nytt
- Norske statusetiketter i brukergrensesnittet (Utkast, Innsendt, Under behandling, Returnert for korrigering)
- Lagring av returbegrunnelse og returtidspunkt i databasen
- Entity Framework Core, migrasjoner og demodata
- Automatiserte tester for Domain, Application, Infrastructure og Web (kjøres i CI)
- GitHub Actions for automatisk bygging og testing

### Planlagt


- Autentisering og rollebasert tilgangsstyring
- Avgrensning slik at ALIS bare ser egne saker
- Registrering og avstemming av veiledningstimer
- Utvidede veilederfunksjoner
- Sakshistorikk og sporbarhet
- Dokumentopplasting og beregning av tilskudd
- Revisorgodkjenning
- Frister og varsling
- Integrasjoner mot P360 og Helsedirektoratet

## Teknologi

- .NET 10
- ASP.NET Core MVC og Razor Views
- Entity Framework Core
- SQL Server Express LocalDB
- xUnit
- GitHub Actions

## Prosjektstruktur

- `Alisflyt.Domain` inneholder entiteter, statuser og forretningsregler.
- `Alisflyt.Application` inneholder brukstilfeller, tjenester, DTO-er og kontrakter.
- `Alisflyt.Infrastructure` inneholder EF Core, databasekontekst, repositories, migrasjoner og demodata.
- `Alisflyt.Web` inneholder controllere, ViewModels, Razor Views og statiske filer.
- `Alisflyt.Integrations` er klargjort for fremtidige integrasjoner mot eksterne systemer.
- `tests` inneholder enhets- og integrasjonstestprosjektene.

Kort huskeregel:

> Web viser. Application koordinerer. Domain bestemmer. Infrastructure lagrer. Integrations kommuniserer.

## Forutsetninger

- Git
- .NET 10 SDK
- Visual Studio med ASP.NET-workload, eller et annet egnet utviklingsmiljø
- SQL Server Express LocalDB på Windows

## Klone repository

```powershell
git clone <repository-url>
cd Alisflyt
```

Erstatt `<repository-url>` med repository-adressen fra GitHub.

## Første gangs oppsett

Gjenopprett det lokale .NET-verktøyet og NuGet-pakkene:

```powershell
dotnet tool restore
dotnet restore
```

Kontroller om LocalDB-instansen finnes:

```powershell
sqllocaldb info
```

Hvis `MSSQLLocalDB` ikke finnes, oppretter du den:

```powershell
sqllocaldb create MSSQLLocalDB
```

Start instansen:

```powershell
sqllocaldb start MSSQLLocalDB
```

## Bygg og test

```powershell
dotnet build Alisflyt.slnx --verbosity minimal
dotnet test Alisflyt.slnx --no-build --verbosity minimal
```

Løsningen inneholder automatiserte tester for Domain, Application, Web og Integration som kjøres i CI.

## Database

Lokal utvikling bruker:

- Instans: `(localdb)\MSSQLLocalDB`
- Database: `AlisflytDevelopment`
- Konfigurasjon: `Alisflyt.Web/appsettings.Development.json`

I Development-miljøet kjører applikasjonen migrasjoner automatisk ved oppstart. Demodata legges inn når `SeedDemoData` er satt til `true`. Integrasjonstestene bruker egne midlertidige LocalDB-databaser og skal ikke bruke `AlisflytDevelopment`.

Databasen kan også oppdateres manuelt:

```powershell
dotnet ef database update --project Alisflyt.Infrastructure/Alisflyt.Infrastructure.csproj --startup-project Alisflyt.Web/Alisflyt.Web.csproj --context Alisflyt.Infrastructure.Persistence.ApplicationDbContext
```

## Starte løsningen

```powershell
dotnet run --project Alisflyt.Web/Alisflyt.Web.csproj
```

Terminalen viser adressen applikasjonen lytter på. Stopp den med `Ctrl+C`.

Du kan også starte `Alisflyt.Web` som oppstartsprosjekt fra Visual Studio.

## Søknadsskjema

Velg **Gå til ALIS → Ny søknad** for samme totrinnsskjema som i Tilskuddsapp:

1. Søknadsopplysninger med lege, ALIS-avtale, stillingsperioder og refusjonsutgifter.
2. Veiledningsattest med veileder, veiledningsøkter, timeoversikt og sted/dato.

**Lagre utkast** lagrer begge trinnene. Lenken **Til saksoversikt og innsending** åpner saken for innsending. **Rediger** åpner begge trinnene igjen, også når saken er returnert for korrigering. ALIS og koordinator kan bruke **Vis hele søknaden og veiledningsattesten** for lesetilgang.

Eksisterende utkast beholder HPR-nummer og ansettelsesperiode, som vises som første stillingsrad. Nye skjemadata lagres i `GrantCases.ApplicationDataJson`. Migrasjonen `AddGrantApplicationData` kjøres automatisk ved oppstart i Development; i andre miljøer brukes migrasjonskommandoen ovenfor.

HPR-oppslag, tilskuddsberegning og elektronisk signering er fortsatt ikke tilgjengelig.

## GitHub Actions

Ved push til `main` og pull requests mot `main` kjører GitHub Actions automatisk:

- Domain-, Application- og Web-tester på Ubuntu
- LocalDB-integrasjonstester på Windows

Begge jobbene skal normalt være grønne før en pull request merges.

## Sikkerhet og testdata

- Ikke legg secrets, passord eller produksjons-connection strings i Git.
- Ikke bruk ekte personopplysninger i utvikling eller demonstrasjon.
- Bruk kun syntetiske testdata.
- Produksjonsklar autentisering og autorisasjon er ikke implementert.

Merk: Dette er en prototype. ALIS ser foreløpig demo-/testdata og er ikke filtrert på innlogget bruker. Autentisering, autorisasjon og produksjonsintegrasjoner er ikke ferdigstilt.

## Videre lesing

- [Bidra til ALISflyt](CONTRIBUTING.md)
- [Arkitektur](docs/architecture.md)
