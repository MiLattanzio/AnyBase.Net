# Changelog

Le modifiche rilevanti di AnyBase.Net sono documentate in questo file.
Il formato segue [Keep a Changelog](https://keepachangelog.com/it-IT/1.1.0/)
e il progetto usa [Semantic Versioning](https://semver.org/lang/it/).

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

[1.1.1]: https://github.com/MiLattanzio/AnyBase.Net/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/MiLattanzio/AnyBase.Net/releases/tag/v1.1.0
