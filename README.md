# AnimationCreator

Unity tabanlı futbol maç replay simülatörü. Harici tracking verisini **FRDS** (Football Replay Data Standard) formatında alır; 3D saha üzerinde oyuncu ve top hareketini canlandırır.

**Unity:** 6000.3.18f1

## Demo

<video src="https://raw.githubusercontent.com/burakttorun/AnimationCreator/main/Docs/ReadmeClip.mp4" controls width="100%"></video>

Auckland FC vs Newcastle Jets — 3D simülasyon ve yayın videosu yan yana (8 sn @ 10 Hz, SkillCorner tracking).

**Kontroller:** Space — oynat / duraklat · **C** — kamera modu

Unity'de denemek için: `Assets/Scenes/SkillCornerDemo.unity` → Play

---

## Girdi veri formatı — bize bu formatta geliyor

Yukarıdaki ekran kaydı, aşağıdaki **FRDS** verisinin Unity'de simüle edilmiş halidir. Ham tracking verisi bu şemaya dönüştürülür; uygulama verilen pozisyonları sahada çizer — hazır animasyon clip'i oynatmaz.

```
Harici veri (SkillCorner JSONL, SoccerNet etiketleri, …)
        │
        ▼
Tools/convert_*_to_frds.mjs
        │
        ▼
StreamingAssets/<sequenceId>/     ← Unity'nin okuduğu girdi
        │
        ▼
MatchDirector + StickFigurePlayer  ← 3D replay simülasyonu
        │
        └─ BroadcastVideoPanel (opsiyonel yayın videosu)
```

### Klasör yapısı

Her sekans `Assets/StreamingAssets/<sequenceId>/` altında üç dosyadan oluşur:

```
<sequenceId>/
├── manifest.json    # maç meta, saha, takımlar, oyuncular, zamanlama
├── events.json      # pas, şut vb. seyrek olaylar
└── frames.jsonl     # her satır bir frame (JSON Lines)
```

| Dosya | Ne içerir |
|-------|-----------|
| `manifest.json` | Saha boyutu, FPS, takım renkleri, oyuncu listesi, isteğe bağlı `dataSource` |
| `events.json` | `type`, `frameIndex`, `actorPlayerId` … |
| `frames.jsonl` | Her frame: top pozisyonu + oyuncu başına joint koordinatları |

Detaylı şema: [`Docs/FRDS-v0.1-spec.md`](Docs/FRDS-v0.1-spec.md)

### Koordinat sistemi

- Birim: **metre**
- Origin: saha merkezi `(0, 0, 0)`
- **X** — saha genişliği · **Z** — saha uzunluğu (kaleler ±Z) · **Y** — yükseklik

### Örnek `manifest.json`

```json
{
  "schemaVersion": "0.2.0",
  "matchId": "skillcorner_demo",
  "pitch": { "lengthM": 104, "widthM": 68 },
  "timing": { "frameRateHz": 10, "durationSeconds": 8 },
  "teams": [
    { "teamId": "home", "name": "Auckland FC", "color": "#2800f0" },
    { "teamId": "away", "name": "Newcastle United Jets FC", "color": "#ffffff" }
  ],
  "players": [
    { "playerId": "p38673", "teamId": "home", "jerseyNumber": 10, "displayName": "G. May" }
  ],
  "dataSource": {
    "type": "skillcorner_tracking",
    "clipId": "1886347",
    "frameRange": { "start": 5000, "end": 5079 }
  }
}
```

### Örnek `frames.jsonl` (tek satır, kısaltılmış)

```json
{
  "frameIndex": 0,
  "timestampMs": 0,
  "ball": { "pos": { "x": 29.1, "y": 0.72, "z": 27.33 } },
  "players": [
    {
      "playerId": "p38673",
      "joints": {
        "pelvis":       { "x": 25.345, "y": 0.914, "z": 8.475 },
        "torso":        { "x": 25.345, "y": 1.423, "z": 8.475 },
        "head":         { "x": 25.345, "y": 1.86,  "z": 8.475 },
        "leftShoulder": { "x": 25.068, "y": 1.314, "z": 8.361 },
        "rightShoulder":{ "x": 25.622, "y": 1.314, "z": 8.589 },
        "leftKnee":     { "x": 25.247, "y": 0.46,  "z": 8.456 },
        "rightKnee":    { "x": 25.428, "y": 0.46,  "z": 8.531 },
        "leftFoot":     { "x": 25.2,   "y": 0.04,  "z": 8.459 },
        "rightFoot":    { "x": 25.459, "y": 0.04,  "z": 8.565 }
      }
    }
  ]
}
```

Her frame'de tüm sahada aktif oyuncular listelenir; joint isimleri sabittir (`pelvis`, `torso`, `head`, diz/ayak, opsiyonel omuz/dirsek).

### Örnek `events.json`

```json
{
  "events": [
    {
      "eventId": "sc_1",
      "type": "pass",
      "frameIndex": 1,
      "timestampMs": 100,
      "actorPlayerId": "p735578"
    }
  ]
}
```

---

## Repodaki demolar

| Sekans | Sahne | Kaynak |
|--------|-------|--------|
| `skillcorner_demo` | `SkillCornerDemo.unity` | SkillCorner Open Data (maç 1886347) |
| `match_clip_demo` | `MatchClipDemo.unity` | SoccerNet Game State |
| `demo_sequence` | Prototype | Prosedürel örnek (`Tools/generate_demo_sequence.mjs`) |

### Kendi verinizi dönüştürme

```bash
# SkillCorner
node Tools/download_skillcorner.mjs
node Tools/convert_skillcorner_to_frds.mjs

# SoccerNet Game State
node Tools/convert_soccernet_gs_to_frds.mjs
```

Çıktı `Assets/StreamingAssets/<sequenceId>/` altına yazılır; `MatchDirector` bileşeninde `sequenceId` alanını eşleştirin.

---

## Mimari (kısa)

| Bileşen | Görev |
|---------|--------|
| `MatchDataLoader` | FRDS dosyalarını okur |
| `MatchDirector` | Zaman çizelgesi, oynatma, interpolasyon |
| `StickFigurePlayer` / `TrackingMarkerPlayer` | Joint pozisyonlarını 3D'de çizer |
| `BroadcastVideoPanel` | Yayın videosunu sim ile senkronlar |

---

## Lisans ve veri kaynakları

- SkillCorner demo verisi: [SkillCorner Open Data](https://github.com/SkillCorner/opendata) lisansına tabidir (`manifest.dataSource.license`).
- SoccerNet verisi kendi lisans koşullarına tabidir.
