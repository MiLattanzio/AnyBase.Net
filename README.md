# AnyBase.Net

[![CI](https://github.com/MiLattanzio/AnyBase.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/MiLattanzio/AnyBase.Net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AnyBase.Net.svg)](https://www.nuget.org/packages/AnyBase.Net)
[![Tool](https://img.shields.io/nuget/v/AnyBase.Net.Tool.svg?label=dotnet%20tool)](https://www.nuget.org/packages/AnyBase.Net.Tool)
[![Playground](https://img.shields.io/badge/playground-WebAssembly-c9ff65)](https://milattanzio.github.io/AnyBase.Net/)

Codifica e decodifica byte o testo UTF-8 usando un alfabeto ordinato qualsiasi.
Ogni byte è rappresentato con una larghezza fissa, quindi il round-trip è
deterministico anche con basi non potenze di due.

La versione 1.2.0 usa `NumeralSystems.Net` 5.3.0 e verifica automaticamente la
compatibilità dell'API pubblica con la versione 1.1.1.

## Installazione

```console
dotnet add package AnyBase.Net --version 1.2.0
```

## Uso

L'ordine dei simboli definisce il loro valore: il primo vale zero, il secondo
uno e così via. Le factory coprono i casi comuni. L'alias distingue il tipo
factory dal namespace radice omonimo quando il codice si trova nel namespace
globale:

```csharp
using AnyBase.Net;
using AnyBaseFactory = global::AnyBase.Net.AnyBase;

var hexadecimal = AnyBaseFactory.CreateHex();

var encoded = hexadecimal.EncodeToString("Ciao 🌍");
// 4369616F20F09F8C8D

var decoded = hexadecimal.DecodeToString(encoded);
// Ciao 🌍
```

Il catalogo `AnyBaseAlphabets` contiene gli alfabeti `Binary`, `Octal`,
`Decimal`, `Hexadecimal`, `Base32`, `Base64` e `Base64Url`. I preset Base32 e
Base64 adottano l'ordine dei simboli di RFC 4648, ma non il suo bit packing:
AnyBase.Net continua a rappresentare separatamente ogni byte a larghezza fissa.

Sono supportati anche alfabeti e byte arbitrari:

```csharp
var binary = AnyBaseFactory.Create("01");
var source = new byte[] { 0, 1, 127, 128, 255 };

var symbols = binary.Encode(source);
var roundTrip = binary.DecodeToBytes(symbols);
```

## Decodifica senza eccezioni

Le estensioni `TryDecodeToBytes` e `TryDecodeToString` restituiscono `false` e
un risultato vuoto se l'input non è valido:

```csharp
if (hexadecimal.TryDecodeToBytes("00FF", out var bytes))
{
    // bytes: 0x00, 0xFF
}
```

Le estensioni sono additive: `IBase<T>` non ha nuovi membri obbligatori e le
implementazioni esistenti restano compatibili.

## Simboli multicarattere e separatori

Le API testuali senza separatore richiedono rappresentazioni non vuote, uniche
e prefix-free. Per esempio, `"a"` e `"ab"` sono ambigui se concatenati. Le API
ad array mantengono sempre i confini; in alternativa, la 1.2.0 permette di
specificare un separatore esatto:

```csharp
var codec = AnyBaseFactory.Create(new[] { "a", "ab" });

var encoded = codec.EncodeToString("A", "|");
// a|ab|a|a|a|a|a|ab

var decoded = codec.DecodeToString(encoded, "|");
// A
```

Il separatore deve essere non vuoto e non può comparire nel testo di un
simbolo.

## Comparatori e validazione

Un comparatore personalizzato controlla sia l'unicità sia il lookup:

```csharp
var codec = AnyBaseFactory.Create(
    new[] { "zero", "one" },
    StringComparer.OrdinalIgnoreCase);
```

`AlphabetValidator` espone la stessa validazione senza costruire il codec:

```csharp
var result = AlphabetValidator.Validate(new[] { "a", "ab" });

Console.WriteLine(result.IsValid);          // true: le API ad array funzionano
Console.WriteLine(result.IsTextCompatible); // false: concatenazione ambigua

foreach (var diagnostic in result.Diagnostics)
{
    Console.WriteLine($"{diagnostic.Kind}: {diagnostic.Message}");
}

var separated = AlphabetValidator.ValidateWithSeparator(
    new[] { "a", "ab" },
    "|");
// separated.IsTextCompatible == true
```

## Playground WebAssembly

Il [playground Blazor WebAssembly](https://milattanzio.github.io/AnyBase.Net/)
esegue tutto localmente nel browser e permette di:

- scegliere uno dei sette preset o definire un alfabeto personalizzato;
- configurare un separatore;
- validare l'alfabeto in tempo reale con diagnostica dettagliata;
- codificare e decodificare testo UTF-8;
- condividere preset, alfabeto, separatore e modalità tramite URL;
- invertire la trasformazione e copiare il risultato.

Per avviarlo in locale:

```console
dotnet run --project AnyBase.Net/AnyBase.Net.Playground
```

## Tool da riga di comando

Installa il .NET global tool:

```console
dotnet tool install --global AnyBase.Net.Tool --version 1.2.0
```

Il comando installato è `anybase`:

```console
anybase encode "Hello" --alphabet hex
# 48656C6C6F

anybase decode 48656C6C6F --alphabet hex
# Hello

anybase encode "A" --alphabet hex --separator "-"
# 4-1

anybase encode "Hello" --alphabet base64url
```

I preset sono `binary`, `octal`, `decimal`, `hex`, `base32`, `base64` e
`base64url`. `--alphabet` accetta anche una sequenza personalizzata; `--base`
mantiene l'alfabeto storico per basi da 2 a 64. Sono inoltre disponibili
`--input <file|->`, `--output <file|->` e stdin. Usa `anybase --help` per la
sintassi completa.

## Sviluppo

```console
dotnet restore AnyBase.Net/AnyBase.Net.sln
dotnet build AnyBase.Net/AnyBase.Net.sln --configuration Release
dotnet test AnyBase.Net/AnyBase.Net.sln --configuration Release
dotnet pack AnyBase.Net/AnyBase.Net/AnyBase.Net.csproj --configuration Release --output artifacts/packages
dotnet pack AnyBase.Net/AnyBase.Net.Tool/AnyBase.Net.Tool.csproj --configuration Release --output artifacts/packages
# PowerShell 7 (Windows, Linux o macOS)
pwsh ./eng/Test-Packages.ps1 -PackageDirectory artifacts/packages -Version 1.2.0

# Windows PowerShell
powershell -ExecutionPolicy Bypass -File ./eng/Test-Packages.ps1 -PackageDirectory artifacts/packages -Version 1.2.0
```

La suite copre l'intero intervallo dei byte, Unicode UTF-8, alfabeti custom,
input malformati e il contratto della CLI. FsCheck esegue inoltre round-trip
generativi variando byte, ordine e dimensione degli alfabeti e padding.

Le modifiche di ogni versione sono raccolte nel [changelog](CHANGELOG.md).
Le istruzioni per GitHub Pages e Trusted Publishing sono in
[docs/RELEASING.md](docs/RELEASING.md).

## Licenza

[MIT](LICENSE)
