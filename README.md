# ALISflyt

ALISflyt er en demonstrasjonsapplikasjon for digital søknad og behandling av kommunale ALIS-tilskudd. Prosjektet er bygget som en fungerende prototype der søknadsflyten, beregning og saksbehandling er modellert i kode.

Målet er å samle søknadsopplysninger, beregningsgrunnlag og saksbehandling i én arbeidsflyt, slik at informasjon kan registreres én gang og gjenbrukes videre i prosessen. I dagens versjon er dette fokusert på en demo-/testmiljø uten reell autentisering.

## Status nå

Dette er en fungerende prototype i demoform, ikke et ferdig produksjonssystem.

### Implementert i repoet

#### Portal og demoroller

- Felles demoportal med tilgang til ALIS, koordinator og veileder
- Navigasjon mellom roller i en demo-portal
- Oversiktsider for ALIS- og koordinatorflyt
- Veilederområdet er kun en enkel placeholder og ikke en ferdig arbeidsflyt

Det finnes ingen reell innlogging, autentisering eller tilgangsstyring. Rollenavigasjonen er kun designet som en demo for å vise ulike brukerflater i samme løsning.

#### Søknadsflyt

- Opprettelse og lagring av ufullstendige utkast
- To-trinns søknadsskjema med registrering av:
  - lege- og spesialiseringsopplysninger
  - type tilskudd
  - stillingstyper
  - én eller flere stillingsperioder
  - ALIS-avtale
  - refusjons- og kostnadsopplysninger
  - sentralitetsopplysninger
  - veiledningsattest og veiledningsøkter
- Redigering og gjenåpning av lagrede søknadsutkast
- Validering før innsending
- Innsending av ferdig søknad

Søknadsdata lagres samlet i `GrantCases.ApplicationDataJson`.

#### Saksbehandling

- Koordinator kan se oversikt over innsendte saker
- Koordinator kan åpne en sak i detalj
- Koordinator kan starte behandling
- Koordinator kan returnere en sak for korrigering med obligatorisk begrunnelse
- Returbegrunnelse og tid for retur lagres
- ALIS kan se forklaring på retur og korrigere søknaden
- Samme innsendte data brukes videre i behandlingsflyten

#### Automatisk beregning

Det er implementert kalkulering basert på søknadsdata.

Beregningen omfatter foreløpig:

- kompensasjon ved fravær
- læringsaktiviteter
- produktivitetselement
- veiledning
- tilrettelegging
- sentralitetstillegg
- samlet beregnet tilskuddsbeløp

Beregningen velger satssett basert på tilskuddsperioden, og kalkulasjonen vises i både søknadsskjemaet og saksdetaljer. Dersom beregningen ikke kan gjennomføres, vises en forklaring i UI-et.

### Rammer for beregningen i prototypen

Prototypen bruker per nå satssettet:

**01.06.2025–31.05.2026**

Hvis tilskuddsperioden ligger utenfor dette intervallet, finnes det ikke et gyldig satssett i prototypen, og beregningen blir derfor ikke tilgjengelig.

Sentralitetstillegg beregnes i denne demoen når:

- kommunen er registrert med sentralitetsgrad 6
- søknaden gjelder tilskudd til ALIS-avtale inkl. veiledning
- det er registrert et søkt beløp
- tilskuddsperioden gir et beregningsgrunnlag

Merkostnader til veiledning inngår ikke i automatisk beregning i denne versjonen.

## Begrensninger / ikke implementert

Dette er fortsatt en prototype. Følgende er ikke ferdig implementert:

- reell autentisering og rollebasert tilgangsstyring
- ekt brukerinlogging for ALIS, koordinator og veileder
- avgrensning slik at ALIS bare ser egne saker
- full veilederflyt og bekreftelse av veiledningsattest
- registrering og avstemming av veiledningstimer
- sakshistorikk og utvidet sporbarhet
- dokumentopplasting
- støtte for flere eller historiske satsperioder
- håndtering av saker som går over flere satsperioder
- revisorgodkjenning
- frister og varsling
- rapportering, eksport og arkivering
- integrasjoner mot eksterne systemer

Prosjektet er løpende utviklet med syntetiske testdata og skal ikke forstås som en komplett eller autoritativ implementasjon av endelig regelverk.

## Teknologi

- .NET 10
- ASP.NET Core MVC
- Razor Views
- Entity Framework Core
- SQL Server Express LocalDB
- JavaScript
- Bootstrap
- xUnit
- GitHub Actions

## Prosjektstruktur

- `Alisflyt.Domain` inneholder entiteter, status, søknadsmodeller og forretningsregler.
- `Alisflyt.Application` inneholder brukstilfeller, beregningstjenester, DTO-er og kontrakter.
- `Alisflyt.Infrastructure` inneholder EF Core, databasekontekst, repositories, migrasjoner og demodata.
- `Alisflyt.Web` inneholder controllere, ViewModels, Razor Views og statiske filer.
- `Alisflyt.Integrations` er klart for fremtidige integrasjoner mot eksterne systemer.
- `tests` inneholder automatiserte tester for ulike lag og integrasjonstestene.

Kort huskeregel:

> Web viser. Application koordinerer. Domain bestemmer. Infrastructure lagrer. Integrations kommuniserer.

## Beregningsarkitektur

Beregningen ligger i Application-laget og følger samme lagdeling som resten av løsningen.

Søknadsdata transformeres til et eget beregningsgrunnlag før beregningen utføres. Beregningsreglene mottar satssett og søknadsdata som eksplisitte inputverdier.

Valg av satssett skjer på serversiden basert på tidligste registrerte `FundingFrom`-dato i søknaden.

Beregningsresultatet transporteres tilbake til Web-laget som DTO og vises både:

- som forhåndsvisning i søknadsskjemaet
- i detaljvisningen for saken
- i koordinatorens detaljvisning

Det gjøres ingen beregning i JavaScript eller Razor Views.

## Forutsetninger

- Git
- .NET 10 SDK
- Visual Studio med ASP.NET-workload, eller annet egnet utviklingsmiljø
- SQL Server Express LocalDB på Windows

## Klone og kjør lokalt

```powershell
git clone <repository-url>
cd Alisflyt
dotnet restore
dotnet build
```

Kjør applikasjonen med:

```powershell
dotnet run --project Alisflyt.Web/Alisflyt.Web.csproj
```

## Tester

Kjør testene med:

```powershell
dotnet test
```

Dette rapporterer status for de eksisterende testprosjektene i `tests/`.

## Notat om demo-/prototypestatus

Denne løsningen er ment som et teknisk og funksjonelt konseptbevis for digital søknad og saksbehandling i ALIS-sammenheng. Den er tilpasset demonstrasjon og videre utvikling, og ikke et ferdig produkt for produksjonsbruk uten ekstra sikkerhets-, regelverks- og operasjonsarbeid.