# Bidra til ALISflyt

Dette dokumentet beskriver arbeidsflyt og forventninger for samarbeid i ALISflyt.

## Brancher og arbeidsflyt

- Arbeid på egne brancher med prefiksene `feature/`, `fix/`, `test/`, `chore/` eller `docs/`.
- Hold pull requests små og fokuserte. Én logisk endring per pull request.
- Bruk korte og tydelige commit-meldinger, for eksempel `Add guidance registration` eller `Fix submit validation`.
- Åpne pull requests mot `main`.
- Ikke push direkte til `main`.

## Pull requests og gjennomgang

- Beskriv hva som er endret, hvorfor det er endret og hvordan det er testet.
- Kontroller at GitHub Actions er grønn før merge.
- Når begge studentene deltar, skal pull requests normalt gjennomgås av den andre før merge.
- Dersom ingen reviewer er tilgjengelig, skal utvikleren dokumentere egenkontrollen og sikre at CI er grønn før merge.
- Bruk skjermbilder når en endring påvirker brukergrensesnittet.

## Kodekvalitet

- Sikt på null build-advarsler og null build-feil.
- Legg forretningsregler i `Alisflyt.Domain`.
- La `Alisflyt.Application` koordinere brukstilfeller og definere kontrakter.
- Legg database- og EF Core-kode i `Alisflyt.Infrastructure`.
- Hold Web-controllerne tynne og bruk ViewModels for skjermbilder og skjemaer.
- Bruk norsk i brukerrettet tekst og engelske navn i kode.
- Ikke dupliser domeneregler i Web-laget.

## Endringer som må koordineres

Avklar med den andre utvikleren før endringer i:

- domenemodeller og delte enums
- Application-kontrakter
- `ApplicationDbContext`
- EF Core-migrasjoner
- `Program.cs`
- felles layout og navigasjon

Dette reduserer risikoen for konflikter og konkurrerende migrasjoner.

## Tester

- Bruk xUnit.
- Legg tester i testprosjektet som tilsvarer laget som endres.
- Legg til tester for nye forretningsregler og sentral controlleroppførsel.
- Test observerbar oppførsel fremfor private implementasjonsdetaljer.
- Kjør hele testpakken før pull request:

```powershell
dotnet build Alisflyt.slnx --verbosity minimal
dotnet test Alisflyt.slnx --no-build --verbosity minimal
```

Integrasjonstestene bruker SQL Server LocalDB og egne midlertidige databaser. I GitHub Actions kjøres de på en Windows-runner.

## Database og migrasjoner

- Koordiner migrasjoner før de opprettes.
- Kontroller genererte migrasjonsfiler før commit.
- Ikke bruk `EnsureCreated` i produksjonskode.
- Ikke legg lokale databasefiler eller connection strings med secrets i Git.

## Informasjonssikkerhet

- Ikke bruk ekte personopplysninger i kode, tester, demodata, skjermbilder eller commits.
- Ikke legg passord, API-nøkler eller andre secrets i repository.
- Bruk syntetiske testdata.

## Kommunikasjon

Gi beskjed tidlig dersom en endring:

- påvirker andre utvikleres arbeid
- endrer en delt kontrakt
- krever en migrasjon
- endrer planlagt funksjonalitet
- introduserer en ny avhengighet eller pakke

Bruk GitHub Issue, pull request eller avtalt chatkanal til avklaringen.
