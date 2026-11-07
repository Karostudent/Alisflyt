# Arkitektur – ALISflyt

Dette dokumentet beskriver kort hvordan koden er organisert og hvilket ansvar de ulike lagene har.

> Web viser. Application koordinerer. Domain bestemmer. Infrastructure lagrer. Integrations kommuniserer.

## Lagene i løsningen

### Alisflyt.Domain

Inneholder forretningsbegreper, entiteter, statuser, søknadsmodeller og regler. Domain håndhever blant annet validering ved innsending, gyldige statusoverganger, krav om begrunnelse ved retur og krav om godkjent veiledningsattest før saken kan settes klar for revisorkontroll. Domain skal ikke kjenne Web, database eller eksterne systemer.

Eksempler:

- `Alisflyt.Domain/Entities/GrantCase.cs`
- `Alisflyt.Domain/Entities/GuidanceApproval.cs`
- `Alisflyt.Domain/Enums/GrantCaseStatus.cs`
- `Alisflyt.Domain/Forms/GrantApplicationData.cs`

### Alisflyt.Application

Koordinerer brukstilfellene og definerer tjenester, DTO-er, request-modeller og tekniske kontrakter. Her ligger blant annet søknadstjenester, veiledningsflyt og beregningsmotoren. `GrantCalculationApplicationService` velger et aktivt satssett som dekker hele beregningsperioden, mens `GrantCalculationService` utfører beregningene.

Eksempler:

- `Alisflyt.Application/Services/IGrantCaseApplicationService.cs`
- `Alisflyt.Application/Services/GrantCaseApplicationService.cs`
- `Alisflyt.Application/Services/GrantCalculationService.cs`
- `Alisflyt.Application/Abstractions/IGrantCaseRepository.cs`

Application bruker Domain, men skal ikke avhenge av Infrastructure.

### Alisflyt.Infrastructure

Implementerer tekniske løsninger for database og lagring. Her ligger EF Core, databasekontekst, repositories, migrasjoner, saksnummergenerator og utviklingsdata. Søknadsdata og vedlegg lagres i `ApplicationDataJson`; vedleggene lagres forenklet som Base64-innhold. `GrantCaseRepository.ListOverviewAsync` bruker EF Core-projeksjon for å hente metadata til ALIS- og koordinatoroversiktene uten å hente `ApplicationDataJson`.

Eksempler:

- `Alisflyt.Infrastructure/Persistence/ApplicationDbContext.cs`
- `Alisflyt.Infrastructure/Persistence/Repositories/GrantCaseRepository.cs`
- `Alisflyt.Infrastructure/Persistence/Migrations/`

Infrastructure avhenger av Application og Domain og implementerer kontrakter definert i Application.

### Alisflyt.Web

Inneholder ASP.NET Core MVC-controllerne, ViewModels, Razor Views, navigasjon, sesjonsbasert demoautentisering og statiske filer. Controllerne håndhever praktiske rolle- og eierskapskontroller for demoens ALIS-, veileder-, koordinator- og administratorområder. `GrantCaseExcelExport` lager Excel-eksporten fra saks- og beregningsdata.

Eksempler:

- `Alisflyt.Web/Controllers/GrantCasesController.cs`
- `Alisflyt.Web/Controllers/CoordinatorController.cs`
- `Alisflyt.Web/Models/DemoUser.cs`
- `Alisflyt.Web/Exports/GrantCaseExcelExport.cs`
- `Alisflyt.Web/Views/GrantCases/Details.cshtml`

Autentiseringen er en sesjonsbasert demoløsning med forhåndsdefinerte, syntetiske brukere. Den er ikke en produksjonsklar identitets- eller tilgangsløsning.

`Alisflyt.Web/Program.cs` starter applikasjonen og fungerer som composition root. Her kobles Application-kontraktene til implementasjonene i Infrastructure.

### Alisflyt.Integrations

Er klargjort for fremtidig kommunikasjon med eksterne systemer, for eksempel P360, Helsedirektoratet og e-posttjenester. Integrasjonene er foreløpig ikke implementert.

### Testprosjektene

Testene ligger under `tests/`:

- `Alisflyt.Domain.Tests` tester forretningsregler, validering, statusoverganger og beregningsrelaterte domeneegenskaper.
- `Alisflyt.Application.Tests` tester brukstilfeller, mapping, beregningsmotor og tjenestelogikk.
- `Alisflyt.Web.Tests` tester controlleroppførsel, rolle-/eierskapskontroller og webflyt.
- `Alisflyt.IntegrationTests` tester Infrastructure og EF Core mot LocalDB, blant annet repositoryprojeksjonen for oversikter.

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

- Sesjonsbasert demoautentisering med syntetiske brukere for ALIS, veileder, koordinator og administrator
- Rolle- og demo-eierskapskontroller i relevante arbeidsområder
- Opprette, lagre og redigere ufullstendige søknadsutkast
- Validere og sende inn søknader
- Veiledningstimer, veiledningsattest og attestgodkjenning
- Ny attestversjon ved veilederens krav om korrigering
- Starte behandling av innsendt søknad
- Returnere søknad for korrigering med begrunnelse og sende den inn på nytt
- Markere saken som `AwaitingAuditorApproval` («Klar for revisorkontroll») når veiledningsattesten er godkjent
- Beregningsmotor for praksiskompensasjon, læringsaktiviteter, lavere produktivitet, veiledningstilskudd, tilrettelegging og sentralitetstillegg
- Separat timebasert veilederhonorar, som ikke inngår i samlet beregnet tilskuddsgrunnlag
- Aktivt satssett som må dekke hele beregningsperioden
- Vedlegg med validering av filtype og størrelse, lagret som Base64 i `ApplicationDataJson`
- Excel-eksport for koordinator for saker i `UnderReview` og `AwaitingAuditorApproval`
- Lettvekts listehenting for ALIS- og koordinatoroversiktene gjennom `ListOverviewAsync`, uten å hente `ApplicationDataJson`
- EF Core-persistens, migrasjoner og utviklingsdata
- Automatiserte tester og GitHub Actions

## Planlagt eller ikke implementert

- Produksjonsautentisering, brukerregister, passordløsning og federert innlogging
- Produksjonsklar tilgangsmodell for sakseierskap og organisatorisk tilhørighet
- Opprettelse, redigering og aktivering av satssett fra administratorområdet
- Historisk lagring av beregningsresultat og permanent kobling til brukt satsversjon
- Støtte for saker som går over flere satsperioder
- Full sakshistorikk, revisjonsspor og komplett vedtaks-/ferdigstillingsflyt
- Selve revisorkontrollen, innsending til direktoratet, vedtak og utbetaling
- Frister, varsling, rapportering og arkivering
- Faglig og teknisk avklart kobling mellom veilederhonorar og tilskudd til veiledning
- Ferdige eksterne integrasjoner mot P360, Helsedirektoratet, HPR eller andre fagsystemer

`Alisflyt.Integrations` tilbyr foreløpig ingen fungerende eksterne integrasjoner. Prosjektet er kun klargjort for fremtidige adaptere.

Se `Alisflyt.Web/Program.cs` for hvordan applikasjonen settes sammen, og `README.md` for oppstart og lokal utvikling.

### Returflyt (kort)

Flyt for retur til ALIS:

```
Draft → Submitted → UnderReview → ReturnedForCorrection → Submitted
```

Kort forklaring:

- Domain håndhever gyldige statusoverganger og krav til begrunnelse ved retur.
- Application koordinerer tiltakene StartReviewAsync og ReturnForCorrectionAsync.
- Infrastructure lagrer ReturnReason og ReturnedAtUtc i databasen via EF Core.
- Web presenterer skjemaer, handlinger og norske valideringsmeldinger, men dupliserer ikke domenereglene.

Behold arkitekturregelen: Web viser. Application koordinerer. Domain bestemmer. Infrastructure lagrer. Integrations kommuniserer.
