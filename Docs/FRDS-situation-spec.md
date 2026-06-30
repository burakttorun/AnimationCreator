# FRDS-Situation v0.3 — Anlık Durum Replay Standardı

FIFA SAOT / VAR ofsayt grafiği gibi **tek an** replay'leri için FRDS v0.2 üzerine ek katman.

## Amaç

90 dakikalık maç yerine **3–8 saniyelik** pencere: pas anı, ofsayt çizgisi, ilgili oyuncular. Maksimum **22 oyuncu** (11+11).

## Dosya Yapısı

FRDS v0.2 ile aynı:

```
<sequenceId>/
├── manifest.json   ← situation bloğu eklenir
├── events.json
└── frames.jsonl    ← yalnızca replay penceresi
```

## manifest.json — situation bloğu

```json
{
  "schemaVersion": "0.3.0",
  "matchId": "offside_demo",
  "pitch": { "lengthM": 105.0, "widthM": 68.0 },
  "timing": { "frameRateHz": 25, "durationSeconds": 5.0 },
  "situation": {
    "type": "offside",
    "title": "VAR — Ofsayt incelemesi",
    "description": "Pas anında forvet savunma hattının gerisinde.",
    "keyFrameIndex": 50,
    "kickPoint": { "x": -8.0, "y": 0.11, "z": 34.5 },
    "offsideLine": {
      "z": 41.0,
      "secondLastDefenderPlayerId": "a5",
      "lastDefenderPlayerId": "a4",
      "offsidePlayerId": "h9"
    },
    "replayWindow": {
      "startFrameIndex": 25,
      "endFrameIndex": 85
    }
  },
  "dataSource": {
    "positions": "authored",
    "joints": "synthesized_from_root",
    "note": "Demo verisi; gerçek tracking entegrasyonu için SoccerTrack/SoccerNet kök pozisyonları kullanılabilir."
  },
  "teams": [ ... ],
  "players": [ ... ]
}
```

### Alanlar

| Alan | Açıklama |
|------|----------|
| `situation.type` | `offside`, `goal`, `foul` (v0.3'te yalnızca `offside` tam desteklenir) |
| `keyFrameIndex` | Topa vurulduğu / pasın verildiği frame |
| `kickPoint` | Top konumu pas anında (metre, FRDS koordinatları) |
| `offsideLine.z` | Ofsayt çizgisi — kale çizgisine paralel düzlem (sabit Z) |
| `secondLastDefenderPlayerId` | İkinci son savunmacı (çizgi referansı) |
| `lastDefenderPlayerId` | Son savunmacı (sarı vurgu) |
| `offsidePlayerId` | Ofsayt şüphesi olan oyuncu (kırmızı vurgu) |
| `replayWindow` | Broadcast'te gösterilen frame aralığı |
| `verdict` | Kesin sonuç: `outcome`, `marginM`, `attackerPoint`, `defenderPoint` |

### verdict (SAOT kesin sonuç)

```json
"verdict": {
  "outcome": "offside",
  "marginM": 3.240,
  "attackerPoint": {
    "playerId": "h9",
    "joint": "head",
    "position": { "x": 2.0, "y": 1.69, "z": 44.24 }
  },
  "defenderPoint": {
    "playerId": "a5",
    "joint": "torso",
    "position": { "x": 16.0, "y": 1.44, "z": 41.0 }
  }
}
```

Unity `SaotVerdictPresenter` pas anında otomatik olarak:
- koyu analiz ortamı
- beyaz iskelet overlay + joint koordinat etiketleri (X/Y/Z, 3 ondalık)
- sarı parlayan karar noktası
- **OFSAYT** banner + mesafe (m)
- alt kırmızı timeline çubuğu

gösterir.

## events.json

```json
{
  "events": [
    {
      "eventId": "evt_pass",
      "type": "pass",
      "frameIndex": 50,
      "timestampMs": 2000,
      "actorPlayerId": "h10",
      "targetPlayerId": "h9",
      "foot": "right"
    },
    {
      "eventId": "evt_offside",
      "type": "offside_review",
      "frameIndex": 50,
      "timestampMs": 2000,
      "actorPlayerId": "h9",
      "targetPlayerId": "a5",
      "foot": ""
    }
  ]
}
```

## frames.jsonl

- Yalnızca `replayWindow` süresi (ör. 60 frame @ 25 Hz = 2.4 s) veya tam kliP (5 s)
- `frameIndex` 0'dan başlar
- Her frame: `ball.pos` + tüm sahadaki oyuncuların `joints`

## Koordinat ve zamanlama

FRDS v0.2 ile aynı: metre, origin saha merkezi, X genişlik, Z uzunluk, Y yükseklik, 25 Hz.

## Unity runtime

`SituationDirector` şunları gösterir:

- Sarı yarı saydam **ofsayt düzlemi** (`offsideLine.z`)
- Pas anında **kick point** işareti
- Ofsayt oyuncusu (kırmızı) ve savunma hattı (sarı) vurgusu
- VAR tarzı kamera açıları (genel / yan)
- `Space` oynat/duraklat, `K` pas anına git, `C` kamera değiştir

## Gerçek veri entegrasyonu

1. SoccerTrack / SoccerNet GSR → kök `(x,z)` + forma numarası
2. `Tools/lib/pose_from_root.mjs` → joint sentezi
3. Manuel veya otomatik `situation` + `offsideLine` metadata
4. Maksimum 22 oyuncu; hakemler filtrelenir

## Örnek sequence

`Assets/StreamingAssets/offside_demo/` — `node Tools/generate_offside_situation.mjs` ile üretilir.
