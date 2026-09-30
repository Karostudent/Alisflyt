# ALISflyt

ALISflyt er en prototype for digital søknad og behandling av kommunale ALIS-tilskudd.

Målet er å samle søknadsopplysninger, beregningsgrunnlag og saksbehandling i én arbeidsflyt, slik at informasjon kan registreres én gang og gjenbrukes videre i prosessen. Dette skal gi ALIS bedre oversikt og redusere manuelt arbeid for kommunens koordinator.

Løsningen er under utvikling og skal foreløpig bare brukes med syntetiske testdata.

## Status

### Implementert

#### Portal og roller

- Felles demoportal med inngang for ALIS, koordinator og veileder
- ALIS-dashboard med oversikt over saker
- Koordinator-dashboard med oversikt over mottatte saker
- Presentasjon av planlagt veilederområde
- Rollemarkering i relevante visninger
- Sortering på saksnummer, HPR, status og sist endret i saksoversiktene

Veilederrollen er foreløpig kun representert som planlagt funksjonalitet. Egen innlogging og arbeidsflyt for veileder er ikke implementert.

#### Søknad og utkast

- Opprettelse og lagring av ufullstendige utkast
- Totrinnsskjema for søknadsopplysninger og veiledningsattest
- Registrering av:
  - lege- og spesialiseringsopplysninger
  - type tilskudd
  - stillingstyper
  - én eller flere stillingsperioder
  - ALIS-avtale
  - refusjons- og kostnadsopplysninger
  - sentralitetsopplysninger
  - veiledningsattest og veiledningsøkter
- Redigering og gjenåpning av lagrede utkast
- Validering før innsending
- Innsending av ferdig søknad

Søknadsdata lagres samlet i `GrantCases.ApplicationDataJson`.

#### Saksflyt

- Koordinator kan åpne og lese innsendte søknader
- Koordinator kan starte behandling
- Koordinator kan returnere en sak for korrigering med obligatorisk begrunnelse
- Returbegrunnelse og returtidspunkt lagres
- ALIS kan se hvorfor saken er returnert
- ALIS kan korrigere og sende saken inn på nytt
- Samme søknadsdata brukes videre gjennom arbeidsflyten

#### Automatisk beregning av tilskudd

Det er implementert automatisk beregning basert på opplysningene i søknaden.

Beregningen omfatter foreløpig:

- kompensasjon ved fravær
- læringsaktiviteter
- produktivitetselement
- veiledning
- tilrettelegging
- sentralitetstillegg
- samlet beregnet tilskuddsbeløp

Beregningen bruker et versjonert satssett som velges ut fra tilskuddsperioden.

ALIS kan se en forhåndsvisning av beregningen før søknaden sendes inn. Beregningsresultatet vises også i detaljvisningen av saken.

Dersom beregningen ikke kan gjennomføres, vises en forklaring i brukergrensesnittet. For sentralitetstillegg vises det også forklaring når beløpet blir 0 på grunn av gjeldende beregningsforutsetninger.

### Rammer for beregningen i prototypen

Prototypen bruker foreløpig satssettet:

**01.06.2025–31.05.2026**

Hvis tilskuddsperioden starter utenfor denne perioden, finnes det ikke et gyldig satssett i prototypen, og beregningen blir derfor ikke tilgjengelig.

Sentralitetstillegg beregnes i dagens prototype når:

- kommunen er registrert med sentralitetsgrad 6
- søknaden gjelder tilskudd til ALIS-avtale inkl. veiledning
- det er registrert et søkt beløp
- tilskuddsperioden gir et beregningsgrunnlag

Registrerte merkostnader til veiledning inngår foreløpig ikke i automatisk beregning fordi beregningsregelen ikke er endelig avklart.

Dette beskriver hva prototypen støtter nå, og skal ikke forstås som en fullstendig eller autoritativ implementasjon av endelig regelverk.

### Planlagt videre

- Autentisering og rollebasert tilgangsstyring
- Avgrensning slik at ALIS bare ser egne saker
- Egen innlogging og arbeidsflate for veileder
- Veilederstyrt registrering og bekreftelse av veiledningsattest
- Registrering og avstemming av veiledningstimer
- Sakshistorikk og utvidet sporbarhet
- Dokumentopplasting
- Videre kvalitetssikring av satser og beregningsregler
- Støtte for flere og historiske satsperioder
- Håndtering av saker som går over flere satsperioder
- Revisorgodkjenning
- Frister og varsling
- Rapportering og eksport
- Arkivering og mulig integrasjon mot P360
- Integrasjon mot Helsedirektoratet
- Tilrettelegging for bruk i flere kommuner

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

- `Alisflyt.Domain` inneholder entiteter, statuser, søknadsmodeller og forretningsregler.
- `Alisflyt.Application` inneholder brukstilfeller, beregningstjenester, DTO-er og kontrakter.
- `Alisflyt.Infrastructure` inneholder EF Core, databasekontekst, repositories, migrasjoner og demodata.
- `Alisflyt.Web` inneholder controllere, ViewModels, Razor Views og statiske filer.
- `Alisflyt.Integrations` er klargjort for fremtidige integrasjoner mot eksterne systemer.
- `tests` inneholder automatiserte tester for de ulike lagene og integrasjonstestene.

Kort huskeregel:

> Web viser. Application koordinerer. Domain bestemmer. Infrastructure lagrer. Integrations kommuniserer.

## Beregningsarkitektur

Beregningsfunksjonaliteten følger samme lagdeling som resten av løsningen.

Søknadsdata transformeres til et eget beregningsgrunnlag før beregningen utføres. Beregningsreglene ligger på Application-nivå og mottar satssett og søknadsdata som eksplisitte inputverdier.

Valg av satssett skjer på serversiden basert på tidligste registrerte `FundingFrom` i søknaden.

Beregningsresultatet transporteres tilbake til Web-laget som DTO og vises både:

- som forhåndsvisning i søknadsskjemaet
- i detaljvisningen for saken
- i koordinatorens detaljvisning

Det gjøres ingen beregning i JavaScript eller Razor Views.

## Forutsetninger

- Git
- .NET 10 SDK
- Visual Studio med ASP.NET-workload, eller et annet egnet utviklingsmiljø
- SQL Server Express LocalDB på Windows

## Klone repository

```powershell
git clone <repository-url>
cd Alisflyt