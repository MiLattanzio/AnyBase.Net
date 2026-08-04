# Changelog

Le modifiche rilevanti di AnyBase.Net sono documentate in questo file.
Il formato segue [Keep a Changelog](https://keepachangelog.com/it-IT/1.1.0/)
e il progetto usa [Semantic Versioning](https://semver.org/lang/it/).

## [1.4.0] - 2026-08-04

### Added

- `EncodingMode.FixedWidthByte` e `EncodingMode.Packed`, selezionabili senza
  cambiare il comportamento predefinito compatibile con le versioni 1.x.
- Factory packed conformi a RFC 4648 per Base16, Base32, Base64 e Base64
  URL-safe, con padding configurabile e validazione canonica dei pad bit.
- Conservazione esplicita degli zero iniziali nel bitstream packed.
- Supporto packed incrementale per stream sincroni e asincroni, inclusi buffer
  che dividono i quantum di codifica.
- Opzioni CLI `--mode fixed|packed` e `--padding include|omit`, con preset RFC
  distinti `rfc-base16`, `rfc-base32`, `rfc-base64` e `rfc-base64url`.
- Selettori di modalita e padding nel playground, preset RFC separati e
  condivisione della configurazione completa tramite URL.
- Test sui vettori ufficiali RFC 4648, confronto con `Convert` di .NET, zero
  iniziali, padding, canonicalita, span, stream e pacchetti installati.

### Changed

- `FixedWidthByte` resta la modalita predefinita; le factory storiche
  `CreateBase32`, `CreateBase64` e `CreateBase64Url` non cambiano formato.
- La validazione del pacchetto confronta l'API pubblica con AnyBase.Net 1.3.0.

## [1.3.0] - 2026-08-02

### Added

- API `ReadOnlySpan<T>`, `Span<T>`, `ReadOnlyMemory<T>` e `Memory<T>` per
  codifica e decodifica senza allocazioni intermedie obbligatorie.
- Calcolo esatto delle dimensioni di simboli, byte e testo codificato prima
  dell'allocazione dell'output.
- Codifica e decodifica incrementale di `Stream`, sincrona e asincrona, con
  memoria limitata, `CancellationToken` e stream lasciati aperti.
- Progetto BenchmarkDotNet per confrontare API ad array e buffer; la CI
  pubblica report Markdown e artefatti per ogni commit.
- Modalità binaria reale nella CLI, con stdin/stdout e file raw, formati
  `text`, `binary` ed `hex` e nessun newline aggiunto all'output binario.
- Caricamento file, viste testo/esadecimale/byte, download del risultato,
  dimensioni e rapporto di espansione nel playground WebAssembly.
- Test di dimensionamento, buffer, stream sincroni/asincroni, cancellazione e
  flussi binari end-to-end della CLI e dei pacchetti.

### Changed

- I percorsi ad array usano le nuove primitive basate su span.
- Lo smoke test del global tool verifica byte non testuali senza conversione
  UTF-8 e senza newline indesiderati.
- La validazione del pacchetto mantiene come baseline pubblicata la 1.1.1.

## [1.2.0] - 2026-08-02

### Added

- Catalogo pubblico di alfabeti binario, ottale, decimale, esadecimale,
  Base32, Base64 e Base64 URL-safe.
- Factory `AnyBase.Create(...)`, `CreateHex()` e factory dedicate a ogni preset.
- API `TryDecodeToBytes` e `TryDecodeToString` additive, senza modificare il
  contratto esistente di `IBase<T>`.
- Separatore esplicito per codificare e decodificare alfabeti testuali con
  simboli multicarattere, inclusi quelli non prefix-free.
- Comparatore personalizzabile per validazione e lookup dei simboli.
- `AlphabetValidator` pubblico con diagnostica strutturata, indici dei simboli
  coinvolti e distinzione tra validità strutturale e compatibilità testuale.
- Preset CLI identificabili per nome e opzione `--separator`.
- Validazione in tempo reale e URL condivisibili nel playground WebAssembly.

### Changed

- I preset Base32, Base64 e Base64 URL-safe usano l'ordine dei simboli definito
  da RFC 4648; il formato resta la codifica a larghezza fissa di AnyBase.Net.
- La validazione del pacchetto confronta l'API pubblica con la versione 1.1.1.

## [1.1.1] - 2026-08-02

### Added

- Property-based test con 300 combinazioni generate di byte, alfabeti, basi e
  dimensioni per ogni esecuzione.
- Smoke test end-to-end dei pacchetti locali `AnyBase.Net` e
  `AnyBase.Net.Tool`.
- Matrice CI su Windows e Linux.
- Validazione automatica del pacchetto e della compatibilità dell'API pubblica
  rispetto ad `AnyBase.Net` 1.1.0.
- Documentazione XML delle condizioni di errore delle API pubbliche.

### Changed

- Le API testuali rifiutano alfabeti vuoti, duplicati o non prefix-free con un
  errore che indica i simboli e gli indici coinvolti.
- Gli alfabeti testualmente ambigui restano utilizzabili tramite le API ad array.
- Gli errori di decodifica riportano posizione testuale, gruppo byte, indice e
  offset del simbolo, oltre ai dettagli degli overflow.
- I pacchetti includono README, changelog, simboli e metadati Source Link.

## [1.1.0] - 2026-08-01

### Added

- Tool globale `AnyBase.Net.Tool` con codifica, decodifica, stdin e file.
- Playground Blazor WebAssembly pubblicato su GitHub Pages.
- Pubblicazione automatica di pacchetti e release tramite Trusted Publishing.

### Changed

- Aggiornamento a NumeralSystems.Net 5.3.0.
- Round-trip UTF-8, alfabeti ordinati deterministici e validazione più rigorosa.

[1.4.0]: https://github.com/MiLattanzio/AnyBase.Net/compare/v1.3.0...HEAD
[1.3.0]: https://github.com/MiLattanzio/AnyBase.Net/compare/v1.2.0...v1.3.0
[1.2.0]: https://github.com/MiLattanzio/AnyBase.Net/compare/v1.1.1...v1.2.0
[1.1.1]: https://github.com/MiLattanzio/AnyBase.Net/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/MiLattanzio/AnyBase.Net/releases/tag/v1.1.0
