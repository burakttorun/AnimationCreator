# FRDS v0.2 — Football Replay Data Standard

Prototip ve entegrasyon için resmi veri formatı. Unity `AnimationCreator` bu şemayı okur.

**v0.2 değişiklikleri:** omuz ve dirsek joint'leri eklendi; Unity tarafında clip yerine doğrudan joint replay kullanılır.

## Dosya Yapısı

```
<sequenceId>/
├── manifest.json
├── events.json
└── frames.jsonl
```

| Dosya | Açıklama |
|-------|----------|
| `manifest.json` | Maç meta, saha, takım ve oyuncu tanımları |
| `events.json` | Pas, şut vb. seyrek aksiyonlar |
| `frames.jsonl` | Her satır bir frame (JSON Lines) |

## Koordinat Sistemi

- Birim: **metre**
- Origin: saha merkezi `(0, 0, 0)`
- **X**: saha genişliği (−34 … +34)
- **Z**: saha uzunluğu (−52.5 … +52.5), kaleler ±Z
- **Y**: yükseklik (çim = 0)

## Zamanlama

- `frameRateHz`: önerilen **25**
- `timestampMs = frameIndex × (1000 / frameRateHz)`
- `frameIndex`: 0’dan başlar, art arda artar
- Runtime’da frame’ler arası **interpolasyon** önerilir (25 Hz → akıcı görüntü)

## manifest.json

```json
{
  "schemaVersion": "0.2.0",
  "matchId": "demo_sequence",
  "pitch": { "lengthM": 105.0, "widthM": 68.0 },
  "timing": { "frameRateHz": 25, "durationSeconds": 16.0 },
  "teams": [
    { "teamId": "home", "name": "Home", "color": "#E63946" },
    { "teamId": "away", "name": "Away", "color": "#457B9D" }
  ],
  "players": [
    { "playerId": "h10", "teamId": "home", "jerseyNumber": 10, "displayName": "Playmaker" }
  ]
}
```

## frames.jsonl

Her satır tek bir JSON nesnesi:

```json
{
  "frameIndex": 0,
  "timestampMs": 0,
  "ball": { "pos": { "x": 0.0, "y": 0.11, "z": 0.0 } },
  "players": [
    {
      "playerId": "h10",
      "joints": {
        "pelvis":        { "x": 0.0,  "y": 0.9,  "z": 0.0 },
        "torso":         { "x": 0.0,  "y": 1.4,  "z": 0.0 },
        "head":          { "x": 0.0,  "y": 1.75, "z": 0.05 },
        "leftShoulder":  { "x": -0.22, "y": 1.32, "z": 0.0 },
        "rightShoulder": { "x": 0.22,  "y": 1.32, "z": 0.0 },
        "leftElbow":     { "x": -0.28, "y": 1.18, "z": 0.12 },
        "rightElbow":    { "x": 0.28,  "y": 1.18, "z": -0.12 },
        "leftKnee":      { "x": -0.15, "y": 0.45, "z": 0.1 },
        "rightKnee":     { "x": 0.15,  "y": 0.45, "z": -0.1 },
        "leftFoot":      { "x": -0.2,  "y": 0.05, "z": 0.25 },
        "rightFoot":     { "x": 0.2,   "y": 0.05, "z": -0.25 }
      }
    }
  ]
}
```

### Zorunlu joint'ler (oyuncu başına)

| Joint | Açıklama |
|-------|----------|
| `pelvis` | Kalça / root |
| `torso` | Göğüs |
| `head` | Baş |
| `leftKnee` | Sol diz |
| `rightKnee` | Sağ diz |
| `leftFoot` | Sol ayak |
| `rightFoot` | Sağ ayak |

### v0.2 opsiyonel (önerilen) joint'ler

| Joint | Açıklama |
|-------|----------|
| `leftShoulder` | Sol omuz |
| `rightShoulder` | Sağ omuz |
| `leftElbow` | Sol dirsek |
| `rightElbow` | Sağ dirsek |

Omuz/dirsek yoksa Unity yalnızca bacak + gövde replay yapar (v0.1 uyumluluk).

## events.json

```json
{
  "events": [
    {
      "eventId": "evt_001",
      "type": "pass",
      "frameIndex": 80,
      "timestampMs": 3200,
      "actorPlayerId": "h10",
      "targetPlayerId": "h9",
      "foot": "right"
    }
  ]
}
```

### Event tipleri

| type | Zorunlu alanlar |
|------|-----------------|
| `pass` | actorPlayerId, targetPlayerId, foot |
| `shot` | actorPlayerId, foot |
| `dribble` | actorPlayerId |
| `tackle` | actorPlayerId |

`foot`: `"left"` | `"right"` | `"head"` | `"other"`

## Unity Entegrasyonu

Runtime yolu: `StreamingAssets/<sequenceId>/`

Örnek: `StreamingAssets/demo_sequence/manifest.json`

**Görselleştirme:** `StickFigurePlayer` joint pozisyonlarını doğrudan çizer — tracking verisi kaynak gerçektir, hazır animasyon clip'i kullanılmaz. İleride humanoid model eklendiğinde aynı `IPlayerVisual` arayüzü üzerinden bağlanır.

### Demo verisi

Prosedürel örnek sekans:

```bash
node Tools/generate_demo_sequence.mjs
```

Unity kurulumu: **`AnimationCreator → Setup Prototype Scene`** → Play

## v0.2 Kapsam Dışı

- Canlı stream / WebSocket
- El bileği / parmak joint'leri
- Hız ve rotasyon alanları (sadece pozisyon)
- 90 dakikalık tam maç zorunluluğu

## Geriye Uyumluluk

`schemaVersion: "0.1.0"` dosyaları okunabilir; omuz/dirsek alanları yoksa kollar bind-pose kalır.
