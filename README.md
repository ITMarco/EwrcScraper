# EWRC Scraper — RCH

**Versie 1.5.0**

---

## Gebruik (NL)

De EWRC Scraper helpt de Rally Club Holland (RCH) om te zien welke leden ingeschreven staan bij rally's, om rijders/bijrijders op te zoeken, en om leden te feliciteren bij mijlpalen — alles op basis van [ewrc-results.com](https://ewrc-results.com).

### Stap 1 – Rally's selecteren

1. Start de applicatie (`EwrcScraper.exe`).
2. Selecteer in **Stap 1** de landen waarvoor je rally's wilt zien (standaard: Nederland, België, Duitsland).
3. Kies het jaar met de ◀ ▶ knoppen.
4. Klik **"🔄 Rally lijst ophalen"** — de lijst wordt geladen vanuit de eWRC API.
5. Vink de rally's aan die je wilt vergelijken of kruisen.

### Stap 2 – Ledenlijst laden

1. Ga naar **Stap 2** of klik op **"📂 Ledenlijst laden"** onderaan het scherm.
2. Kies het ledenbestand (CSV of Excel `.xlsx`). Het pad kan ook via **Voorkeuren** worden ingesteld.
3. Het bestand wordt onthouden voor de volgende keer.

**Vereiste kolommen in het ledenbestand:**

| Kolom | Uitleg |
|-------|--------|
| `Leden Nr.` | Lidnummer |
| `Voornaam` | Voornaam |
| `Tussenvoegsel` | Tussenvoegsel (optioneel) |
| `Achternaam` | Achternaam |
| `EwrcNrPilot` | EWRC rijdersnummer |
| `EwrcNrCoPilot` | EWRC bijrijdersnummer |
| `E-mail adres` | E-mailadres (ook `Email` of `emailadres` werkt) |

Andere kolommen worden automatisch herkend en bewaard.

### Stap 3 – Vergelijken en exporteren

1. Klik **"⬇ Rally info ophalen"** — haalt alle inschrijvingen op voor de geselecteerde rally's.
2. Klik **"🔍 Vergelijk met ledenlijst"** — toont welke leden ingeschreven staan.
3. Filter de resultaten met het filterveld.
4. Klik **"📋 Kopieer e-mailadressen"** om alle e-mailadressen naar het klembord te kopiëren.
5. Of exporteer de resultaten naar CSV of Excel.

### EWRC Rijders/Navigator opzoeken

Op deze tab zoek je rijders en bijrijders op naam. Je ziet hun profiel, foto, **EWRC ID**, land, geboortedatum en statistieken per categorie, met een link naar hun eWRC-profiel. Voeg meerdere personen toe aan de lijst om ze naast elkaar te **vergelijken**. Rijders en bijrijders hebben aparte ID-namespaces; het juiste profiel wordt automatisch gekozen.

### 🎉 Gelegenheden

Feliciteer leden bij mijlpalen op basis van hun eWRC-statistieken.

- **Mijlpalen**: aantal rally-starts, totaal gereden kilometers, en jubileumjaren (jaren sinds debuut), plus **verjaardagen** en round-age-momenten.
- **🔄 Ververs statistieken** haalt (alleen verouderde) ledengegevens op van eWRC; **♻ Alles verversen** negeert de cache. Bij het opstarten wordt direct uit de cache getoond, zonder netwerk.
- **⚙ Mijlpalen bewerken** — pas alle drempels aan (starts, km, jubileumjaren, leeftijden, vooraankondiging, "minder actief na X jaar").
- **🔗 Kruis met rally (Stap 1)** — koppelt de in Stap 1 aangevinkte rally aan de leden en benoemt de aankomende rally die iemands mijlpaalstart wordt ("25e start bij GTC Rally op 10 juli!").
- **Statussen**: 🎉 *Nieuw* (net gehaald of aankomende mijlpaalstart), ✓ *Bereikt*, ⏳ *Aankomend*.
- **Per-rol activiteit**: rijder en bijrijder worden apart geteld. Een rol met een laatste rally ouder dan de drempel wordt als *minder actief* gemarkeerd; jubileum en verjaardag worden voor zo'n inactieve rol onderdrukt (feitelijke rally-/km-mijlpalen blijven staan).

> **Belasting van eWRC:** statistieken worden gecachet in `member-stats.json`, alleen verouderde entries worden opnieuw opgehaald, met vertraging tussen aanvragen. Voor de mijlpaaltelling gebruikt de app de results-based `lg_starts` (voltooide starts) in plaats van `number_of_starts`, dat een rally al meetelt zodra iemand is ingeschreven.

### Voorkeuren

Klik op **"⚙ Voorkeuren"** om o.a. het ledenlijst-pad, update-controle en het debug-venster in te stellen.

### Debug venster

Klik op **"🛠 Debug"** rechtsboven om een apart debug-venster te openen. Hier zie je alle API-aanvragen en meldingen in real-time — inclusief update-diagnostiek en per-endpoint statuscodes.

### Bijwerken

De app controleert bij het opstarten op nieuwe versies. Een geïnstalleerde kopie (via Setup.exe) downloadt en start het installatieprogramma; een draagbare kopie vervangt zichzelf via de zip. Handmatig downloaden kan altijd via de release-pagina.

---

## Technische informatie

### Technologie

| Onderdeel | Details |
|-----------|---------|
| Platform | .NET 10, Windows |
| UI | WPF (Windows Presentation Foundation) |
| Patroon | MVVM (CommunityToolkit.Mvvm) |
| HTTP | HttpClient met User-Agent header |
| CSV/Excel | ClosedXML, eigen CSV-parser |
| JSON | Newtonsoft.Json |
| Installer | Inno Setup 6 (self-contained, incl. .NET runtime) |
| Config | `%AppData%\EwrcScraper\` |

### Projectstructuur

```
CSharpScraper/
├── Models/          # Datamodellen: RallyEvent, RchMember, DriverEntry,
│                    #   MemberStats, Occasion, ...
├── Services/        # Business logic: EwrcApiService, MemberListService,
│                    #   MemberStatsService, OccasionService, UpdateService, ...
├── ViewModels/      # MVVM ViewModels per tab
├── Views/           # Extra vensters (Debug, Voorkeuren, UpdateDialog,
│                    #   OccasionSettingsWindow)
├── Converters/      # WPF value converters
├── Resources/       # Icoon en afbeeldingen
├── App.xaml         # Applicatie resources en stijlen
└── MainWindow.xaml  # Hoofdvenster met 5 tabs
Installer/
├── EwrcScraper.iss   # Inno Setup script
└── Build-Release.ps1 # Bouwt zip + Setup.exe voor een release
```

### eWRC API endpoints

| Endpoint | Gebruik |
|----------|---------|
| `/calendar/{jaar}/natall` | Landen ophalen |
| `/calendar/{jaar}/list?nat=...` | Kalender per land |
| `/event/{id}/entries` | Inschrijvingen per rally |
| `/search?query=...` | Rijders/bijrijders zoeken |
| `/driver/{id}` · `/codriver/{id}` | Profiel (naam, land, geboren, `number_of_starts`, `mileage`, seizoenen, titels) |
| `/driver/{id}/categories?all=true` | Statistieken per categorie, incl. results-based `lg_starts` |

> Foto's worden geserveerd vanaf `https://media.ewrc-results.com/photos/{bestand}`.
> Profielpagina's: `/profile/{id}-{slug}/` (rijder) en `/coprofile/{id}-{slug}/` (bijrijder).

### Configuratiebestanden (`%AppData%\EwrcScraper\`)

| Bestand | Inhoud |
|---------|--------|
| `preferences.json` | Venstergrootte/-positie, geselecteerde landen, ledenlijst-pad, update-controle |
| `member-stats.json` | Gecachte eWRC-statistieken per lid (voor Gelegenheden) |
| `occasions.json` | Bewerkbare mijlpaal-drempels |

### Versioning

De versie is vastgelegd in:
- `version.json` — voor update-detectie
- `EwrcScraper.csproj` — `<Version>` property
- `AssemblyVersion` / `FileVersion` — zichtbaar in Windows bestandseigenschappen

### Bouwen

```bash
# Debug build
dotnet build

# Draaien vanuit broncode
dotnet run -c Release

# Volledige release (framework-dependent zip + standalone Setup.exe)
./Installer/Build-Release.ps1
```

Het release-script leest de versie uit `version.json`, bouwt een framework-dependent
zip en een standalone installer, en toont welke bestanden naar de GitHub-release moeten.

### Milestones

| Milestone | Status | Inhoud |
|-----------|--------|--------|
| **M1** | ✅ Gereed | C# WPF conversie, debug venster, versioning, README |
| **M2** | ✅ Gereed | Update-detectie, voorkeuren-venster, installer-bewuste auto-update, landenselectie opslaan |
| **M3** | ✅ Gereed | EWRC opzoeken (ID/foto/vergelijken), Gelegenheden-tab (mijlpalen, jubilea, verjaardagen, rally-kruisverwijzing) |

---

*Gemaakt voor de Rally Club Holland (RCH). Broncode: [github.com/ITMarco/EwrcScraper](https://github.com/ITMarco/EwrcScraper)*
