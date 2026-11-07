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
- Koordinator kan starte behandling av innsendte saker (`Submitted` → `UnderReview`)
- Koordinator kan returnere en sak for korrigering med obligatorisk begrunnelse
- Returbegrunnelse og tidspunkt lagres og vises til ALIS
- ALIS kan åpne, korrigere og sende inn en returnert søknad på nytt (`ReturnedForCorrection` → `Submitted`)
- Veileder får oversikt over tilknyttede ALIS-leger og deres veiledningsattester
- Veileder kan se veiledningsøkter og beregnet veiledningsbeløp
- Veileder kan godkjenne en ventende attest eller kreve korrigering
- Korrigeringsflyten oppretter en ny attestversjon ved endring av veiledningsdata
- Godkjenningsstatusene `Pending`, `NeedsCorrection`, `Approved` og `Superseded` vises i løsningen
- Når veiledningsattesten er godkjent, kan koordinator markere saken som `AwaitingAuditorApproval` («Klar for revisorkontroll»)
- Koordinator kan se veiledergodkjenningen i saksdetaljen og eksportere saker under behandling til Excel

Den sentrale saksflyten er: `Draft` → `Submitted` → `UnderReview`. En sak kan returneres som `ReturnedForCorrection` og sendes inn på nytt. Siste implementerte steg er `AwaitingAuditorApproval` («Klar for revisorkontroll»). Senere statuser finnes i domenemodellen, men selve revisorkontrollen, vedtaket og utbetalingen er ikke implementert.

#### Administrator

Administratorområdet er en presentasjonsvisning som:

- viser alle saker i løsningen
- viser registrerte satssett, gyldighetsperioder og aktiv/inaktiv status

Opprettelse, redigering og aktivering av satssett er ikke implementert i Web-laget.

#### Automatisk beregning

Det er implementert kalkulering basert på søknadsdata. Beregningen gjøres i Application-laget og vises i søknadsskjemaet, saksdetaljer og koordinatorens detaljvisning.

Beregningen omfatter foreløpig:

- **Praksiskompensasjon:** manuelt oppgitt beløp, begrenset til beregnet maksimalramme
- **Læringsaktiviteter:** manuelt oppgitte kostnader, begrenset til maksimalramme etter periode og stillingsandel
- **Lavere produktivitet:** beregnet for relevante fastlege-/fastlegevikarperioder etter 24-månedersregelen
- **Tilskudd til veiledning:** manuelt oppgitte veiledningskostnader, begrenset til maksimalrammen
- **Tilrettelegging:** beregnet tilskudd etter søknadsdata og satssett
- **Sentralitetstillegg:** søkt beløp, begrenset til maksimalramme, når vilkårene for sentralitetsgrad 6 og riktig tilskuddstype er oppfylt
- **Samlet beregnet tilskuddsgrunnlag:** summerer de ordinære beregnede tilskuddspostene

Praksiskompensasjonens maksimalramme beregnes per stillingsperiode ut fra salærsats, praksiskompensasjonsrate, 160 timer, stillingsandel og antall dager i tilskuddsperioden. Systemet registrerer ikke faktiske fraværstimer eller fraværsdager som beregningsgrunnlag.

Veilederhonoraret beregnes separat og timebasert fra registrerte veiledningstimer, forberedelsesfaktor og sats. Det vises som informasjon, men inngår ikke i samlet beregnet tilskuddsgrunnlag. Ekstra veiledningskostnader registreres også separat og inngår ikke automatisk i totalen.

Beregningen velger satssett basert på tilskuddsperioden. Dersom beregningen ikke kan gjennomføres, vises en forklaring i UI-et.

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

#### Vedlegg

Vedlegg valideres ved lagring og innsending og lagres forenklet som Base64-innhold i `ApplicationDataJson`. Tillatte filtyper er PDF, PNG, JPG og JPEG. Det kan lastes opp maksimalt 30 vedlegg, hvert vedlegg kan være inntil 5 MB, og samlet størrelse kan være inntil 15 MB. En ALIS-avtale er obligatorisk ved innsending av søknadstypen «ALIS-avtale inkludert veiledning». Vedlegg beholdes ved retur og ny innsending, og koordinator kan laste dem ned.

#### Excel-eksport

Koordinator kan eksportere saker til Excel når status er `UnderReview` eller `AwaitingAuditorApproval`. Eksporten bruker den samme beregningsmotoren som visningen, skiller mellom oppgitte beløp og beregnede tilskudd og inneholder beregningsgrunnlag, satssett og revisjonsdetaljer. Eksporten forklarer også at veilederhonoraret ikke inngår i samlet beregnet tilskuddsgrunnlag.

#### Oversiktsytelse

ALIS- og koordinatoroversiktene bruker en lettvekts databaseprojeksjon som bare henter nødvendige metadata. `ApplicationDataJson` og Base64-vedlegg hentes ikke i disse oversiktsspørringene. Saksdetaljer, vedlegg og veilederoversikten bruker fortsatt egne, mer omfattende datahentinger.

## Begrensninger / ikke implementert

Dette er fortsatt en prototype. Følgende er ikke ferdig implementert eller må behandles som avgrensninger:

- produksjonsautentisering, brukerregister, passordløsning og federert innlogging; dagens innlogging er demoautentisering med syntetiske brukere og session
- produksjonsklar tilgangsmodell for sakseierskap og organisatorisk tilhørighet; prototypen bruker rolle- og eierskapskontroller for demoen
- opprettelse, redigering og aktivering av satssett fra administratorområdet
- lagring av et historisk beregningsresultat eller en permanent kobling til brukt satsversjon
- automatisk beregning av alle kostnadstyper, blant annet ekstra veiledningskostnader
- koblingen mellom separat veilederhonorar og tilskudd til veiledning; dette er et faglig avklaringspunkt
- praksiskompensasjon beregnes fra manuelt oppgitt beløp, begrenset av maksimalrammen, og ikke fra registrerte fraværstimer
- støtte for saker som går over flere satsperioder; ett satssett må dekke hele beregningsperioden
- full sakshistorikk, revisjonsspor og vedtaks-/ferdigstillingsflyt
- frister, varsling, rapportering og arkivering
- reelle integrasjoner mot P360, HPR, kommunale fagsystemer eller andre eksterne systemer
- selve revisorkontrollen, innsending til direktoratet, vedtak og utbetaling

Vedlegg er implementert, men lagres som Base64 i søknadsdataene. Dette er en forenklet prototypemodell og ikke en anbefalt produksjonsarkitektur.

Veilederoversikten er ikke optimalisert med samme lettvekts listehenting som ALIS- og koordinatoroversiktene og kan derfor være mer påvirket av store søknadsdata og vedlegg.

Det er sporadisk observert `TaskCanceledException` ved navigasjon eller rollebytte. Dette er ikke bekreftet som en behandlingsfeil og kan skyldes at en HTTP-forespørsel avbrytes, eller at debuggeren stopper på et kansellert databasekall.

Beregninger gjøres på nytt ved visning og eksport med tilgjengelig satssett. Historiske saker kan derfor få et annet beregningsresultat dersom satsdata endres. Ett aktivt satssett må dekke hele beregningsperioden; prototypen støtter ikke beregning over flere satsperioder.

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

Den samlede brukerflyten er også testet manuelt i nettleseren gjennom scenarioer for normal søknadsflyt, veilederkorrigering, retur fra koordinator samt beregninger og Excel-eksport. Alle scenarioene ble bestått. Den siste rapporterte automatiske testkjøringen hadde 387 beståtte tester.

## Notat om demo-/prototypestatus

Denne løsningen er ment som et teknisk og funksjonelt konseptbevis for digital søknad og saksbehandling i ALIS-sammenheng. Den er tilpasset demonstrasjon og videre utvikling, og ikke et ferdig produkt for produksjonsbruk uten ekstra sikkerhets-, regelverks- og operasjonsarbeid.