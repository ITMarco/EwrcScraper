# EWRC Scraper — RCH

**Versie 1.0.0** — Milestone 1

---

## Gebruik (NL)

De EWRC Scraper helpt je om te zien welke leden van de Rally Club Holland (RCH) ingeschreven staan bij rally's via [ewrc-results.com](https://ewrc-results.com).

### Stap 1 – Rally's selecteren

1. Start de applicatie (`EwrcScraper.exe`).
2. Selecteer in **Stap 1** de landen waarvoor je rally's wilt zien (standaard: Nederland, België, Duitsland).
3. Kies het jaar met de ◀ ▶ knoppen.
4. Klik **"🔄 Rally lijst ophalen"** — de lijst wordt geladen vanuit de eWRC API.
5. Vink de rally's aan die je wilt vergelijken.

### Stap 2 – Ledenlijst laden

1. Ga naar **Stap 2** of klik op **"📂 Ledenlijst laden"** onderaan het scherm.
2. Kies het ledenbestand (CSV of Excel `.xlsx`).
3. Het bestand wordt onthouden voor de volgende keer.

**Vereiste kolommen in het ledenbestand:**

| Kolom | Uitleg |
|-------|--------|
| `Leden Nr.` | Lidnummer |
| `Voornaam` | Voornaam |
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
5. Of klik **"💾 Exporteer naar CSV"** om de resultaten op te slaan.

### Rijder zoeken

Ga naar de tab **"EWRC Zoeken"** om rijders en bijrijders op te zoeken bij naam.
Je ziet hun profiel, foto en statistieken per categorie.

### Debug venster

Klik op **"🛠 Debug"** rechtsboven om een apart debug-venster te openen.
Hier zie je alle API-aanvragen en meldingen in real-time.

---

## Technische informatie

### Technologie

| Onderdeel | Details |
|-----------|---------|
| Platform | .NET 9, Windows |
| UI | WPF (Windows Presentation Foundation) |
| Patroon | MVVM (CommunityToolkit.Mvvm) |
| HTTP | HttpClient met User-Agent header |
| CSV/Excel | ClosedXML, eigen CSV-parser |
| Config | Newtonsoft.Json, opgeslagen in `%AppData%\EwrcScraper\preferences.json` |

### Projectstructuur

```
CSharpScraper/
├── Models/          # Datamodellen: RallyEvent, RchMember, DriverEntry, ...
├── Services/        # Business logic: EwrcApiService, MemberListService, ...
├── ViewModels/      # MVVM ViewModels per tab
├── Views/           # Extra vensters (DebugWindow)
├── Converters/      # WPF value converters
├── App.xaml         # Applicatie resources en stijlen
└── MainWindow.xaml  # Hoofdvenster met 4 tabs
```

### eWRC API endpoints

| Endpoint | Gebruik |
|----------|---------|
| `/calendar/{jaar}/natall` | Landen ophalen |
| `/calendar/{jaar}/list?nat=...` | Kalender per land |
| `/event/{id}/entries` | Inschrijvingen per rally |
| `/search?query=...` | Rijders/bijrijders zoeken |
| `/driver/{id}` | Rijdersprofiel |
| `/driver/{id}/categories?all=true` | Statistieken per categorie |

### Voorkeuren

Voorkeuren worden opgeslagen in:
```
%AppData%\EwrcScraper\preferences.json
```

Inclusief: venstergrootte/-positie, geselecteerde landen, pad naar ledenbestand.

### Versioning

De versie is vastgelegd in:
- `version.json` — voor update-detectie (Milestone 2)
- `EwrcScraper.csproj` — `<Version>` property
- `AssemblyVersion` — zichtbaar in Windows bestandseigenschappen

### Bouwen

```bash
# Debug build
dotnet build

# Release (framework-dependent, klein)
dotnet publish -c Release -r win-x64 --no-self-contained -o publish/framework

# Standalone (inclusief .NET runtime, geen installatie nodig)
dotnet publish -c Release -r win-x64 --self-contained -o publish/standalone
```

### Milestones

| Milestone | Status | Inhoud |
|-----------|--------|--------|
| **M1** | ✅ Gereed | C# WPF conversie, debug venster, versioning, README |
| **M2** | 🔲 Gepland | Update-detectie, voorkeuren-venster, auto-update, landenselectie opslaan |

---

*Gemaakt voor de Rally Club Holland (RCH). Broncode: [github.com/ITMarco/EwrcScraper](https://github.com/ITMarco/EwrcScraper)*
