# AnyBase.Net

[![CI](https://github.com/MiLattanzio/AnyBase.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/MiLattanzio/AnyBase.Net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AnyBase.Net.svg)](https://www.nuget.org/packages/AnyBase.Net)
[![Tool](https://img.shields.io/nuget/v/AnyBase.Net.Tool.svg?label=dotnet%20tool)](https://www.nuget.org/packages/AnyBase.Net.Tool)
[![Playground](https://img.shields.io/badge/playground-WebAssembly-c9ff65)](https://milattanzio.github.io/AnyBase.Net/)

Codifica e decodifica byte o testo UTF-8 usando un alfabeto ordinato qualsiasi.
Ogni byte è rappresentato con una larghezza fissa, quindi il round-trip è
deterministico anche con basi non potenze di due.

La versione 1.1.0 usa `NumeralSystems.Net` 5.3.0.

## Installazione

```console
dotnet add package AnyBase.Net --version 1.1.0
```

## Uso

L'ordine dei simboli definisce il loro valore: il primo vale zero, il secondo
uno e così via.

```csharp
using AnyBase.Net;

var hexadecimal = new Base<char>("0123456789ABCDEF");

var encoded = hexadecimal.EncodeToString("Ciao 🌍");
// 4369616F20F09F8C8D

var decoded = hexadecimal.DecodeToString(encoded);
// Ciao 🌍
```

Sono supportati anche i byte arbitrari:

```csharp
var binary = new Base<char>("01");
var source = new byte[] { 0, 1, 127, 128, 255 };

var symbols = binary.Encode(source);
var roundTrip = binary.DecodeToBytes(symbols);
```

Per alfabeti in cui ogni simbolo occupa più caratteri, l'overload che riceve
un array di simboli evita qualsiasi ambiguità. La decodifica da stringa usa il
matching più lungo e richiede rappresentazioni testuali uniche.

## Playground WebAssembly

Il [playground Blazor WebAssembly](https://milattanzio.github.io/AnyBase.Net/)
esegue tutto localmente nel browser e permette di:

- scegliere basi 2, 8, 10, 16, 32 e 64;
- definire un alfabeto personalizzato;
- codificare e decodificare testo UTF-8;
- invertire la trasformazione e copiare il risultato.

Per avviarlo in locale:

```console
dotnet run --project AnyBase.Net/AnyBase.Net.Playground
```

## Tool da riga di comando

Installa il .NET global tool:

```console
dotnet tool install --global AnyBase.Net.Tool --version 1.1.0
```

Il comando installato è `anybase`:

```console
anybase encode "Hello" --base 16
# 48656C6C6F

anybase decode 48656C6C6F --base 16
# Hello

anybase encode "Hello" --alphabet "01"
```

Sono disponibili `--input <file|->`, `--output <file|->`, stdin e basi da 2 a
64. Usa `anybase --help` per la sintassi completa.

## Sviluppo

```console
dotnet restore AnyBase.Net/AnyBase.Net.sln
dotnet build AnyBase.Net/AnyBase.Net.sln --configuration Release
dotnet test AnyBase.Net/AnyBase.Net.sln --configuration Release
```

La suite copre l'intero intervallo dei byte, Unicode UTF-8, alfabeti custom,
input malformati e il contratto della CLI.

Le istruzioni per GitHub Pages e Trusted Publishing sono in
[docs/RELEASING.md](docs/RELEASING.md).

## Licenza

[MIT](LICENSE)
