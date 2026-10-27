# ALISflyt

ALISflyt er en demonstrasjonsapplikasjon for digital søknad og behandling av kommunale ALIS-tilskudd. Prosjektet er bygget som en fungerende prototype der søknadsflyten, beregning og saksbehandling er modellert i kode.

Målet er å samle søknadsopplysninger, beregningsgrunnlag og saksbehandling i én arbeidsflyt, slik at informasjon kan registreres én gang og gjenbrukes videre i prosessen. I dagens versjon er dette fokusert på et demo-/testmiljø uten produksjonsautentisering.

## Status nå

Dette er en fungerende prototype i demoform, ikke et ferdig produksjonssystem.

### Implementert i repoet

#### Portal og demoroller

- Felles demoportal med tilgang til ALIS, veileder, koordinator og administrator
- Demoinnlogging med syntetiske brukere og sesjonsbasert brukeridentitet
- Rollebasert navigasjon til hvert arbeidsområde
- Rolle- og tilknytningskontroller i ALIS-, veileder- og administratorflyten
- Forhåndsdefinert kobling mellom demo-ALIS og demo-veileder

Dette er ikke produksjonsautentisering. Demoidentiteten lagres i session og er kun laget for en kontrollert demonstrasjon med fiktive brukere.

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

#### Saksbehandling og veiledergodkjenning

- Koordinator kan se saksoversikt og åpne saksdetaljer
- Koordinator kan starte behandling av innsendte saker
- Koordinator kan returnere en sak for korrigering med obligatorisk begrunnelse
- Returbegrunnelse og tidspunkt lagres og vises til ALIS
- ALIS kan åpne, korrigere og sende inn en returnert søknad på nytt
- Veileder får oversikt over tilknyttede ALIS-leger og deres veiledningsattester
- Veileder kan se veiledningsøkter og beregnet veiledningsbeløp
- Veileder kan godkjenne en ventende attest eller kreve korrigering
- Korrigeringsflyten oppretter en ny attestversjon ved endring av veiledningsdata
- Godkjenningsstatusene `Pending`, `NeedsCorrection`, `Approved` og `Superseded` vises i løsningen
- Koordinator kan se veiledergodkjenningen i saksdetaljen og eksportere saker under behandling til Excel

#### Administrator

Administratorområdet er en presentasjonsvisning som:

- viser alle saker i løsningen
- viser registrerte satssett, gyldighetsperioder og aktiv/inaktiv status

Opprettelse, redigering og aktivering av satssett er ikke implementert i Web-laget.

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

Dette er fortsatt en prototype. Følgende er ikke ferdig implementert eller må behandles som avgrensninger:

- produksjonsautentisering, brukerregister, passordløsning og federert innlogging
- fullstendig og konsekvent server-side autorisasjon for alle koordinatorhandlinger
- produksjonsklar tilgangsmodell for sakseierskap og organisatorisk tilhørighet
- opprettelse, redigering og aktivering av satssett fra administratorområdet
- lagring av et historisk beregningsresultat eller en permanent kobling til brukt satsversjon
- automatisk beregning av alle kostnadstyper, blant annet merkostnader til veiledning
- støtte for saker som går over flere satsperioder
- filopplasting og lagring av vedlegg
- full sakshistorikk, revisjonsspor og vedtaks-/ferdigstillingsflyt
- frister, varsling, rapportering og arkivering
- reelle integrasjoner mot P360, HPR, kommunale fagsystemer eller andre eksterne systemer
- nettleserbasert ende-til-ende-test av den komplette brukerflyten

Beregninger gjøres på nytt ved visning med tilgjengelig satssett. Historiske saker kan derfor få et annet beregningsresultat dersom satsdata endres.

Prosjektet bruker syntetiske testdata og skal ikke forstås som en komplett eller autoritativ implementasjon av endelig regelverk.

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

Valg av satssett skjer på serversiden basert på tidligste registrerte `FundingFrom`-dato i søknaden. Beregningsresultatet lagres ikke som et historisk snapshot; det beregnes på nytt ved forhåndsvisning, saksdetalj og eksport.

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

Kjør build og test lokalt med:

```powershell
dotnet build Alisflyt.slnx --configuration Release
dotnet test tests/Alisflyt.Domain.Tests/Alisflyt.Domain.Tests.csproj --configuration Release --no-build
dotnet test tests/Alisflyt.Application.Tests/Alisflyt.Application.Tests.csproj --configuration Release --no-build
dotnet test tests/Alisflyt.Web.Tests/Alisflyt.Web.Tests.csproj --configuration Release --no-build
dotnet test tests/Alisflyt.IntegrationTests/Alisflyt.IntegrationTests.csproj --configuration Release --no-build
```

Testprosjektene dekker domain-regler, Application-tjenester, controlleroppførsel og databaseintegrasjon mot LocalDB. GitHub Actions kjører unit-/webtestene på Ubuntu og integrasjonstestene på Windows med LocalDB. Testene er ikke en erstatning for manuell nettleserverifisering av hele demoen.

## Notat om demo-/prototypestatus

Denne løsningen er ment som et teknisk og funksjonelt konseptbevis for digital søknad og saksbehandling i ALIS-sammenheng. Den er tilpasset demonstrasjon og videre utvikling, og ikke et ferdig produkt for produksjonsbruk uten ekstra sikkerhets-, regelverks- og operasjonsarbeid.