# AnyBase.Net

[![CI](https://github.com/MiLattanzio/AnyBase.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/MiLattanzio/AnyBase.Net/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/AnyBase.Net.svg)](https://www.nuget.org/packages/AnyBase.Net)
[![Tool](https://img.shields.io/nuget/v/AnyBase.Net.Tool.svg?label=dotnet%20tool)](https://www.nuget.org/packages/AnyBase.Net.Tool)
[![Playground](https://img.shields.io/badge/playground-WebAssembly-c9ff65)](https://milattanzio.github.io/AnyBase.Net/)

Codifica e decodifica byte o testo UTF-8 usando un alfabeto ordinato qualsiasi.
La modalità predefinita rappresenta ogni byte a larghezza fissa; la modalità
packed opzionale produce bitstream compatti e interoperabili.

La versione 1.4.0 usa `NumeralSystems.Net` 5.3.0 e verifica automaticamente la
compatibilità dell'API pubblica con la versione 1.3.0, l'ultima baseline
pubblicata su NuGet.org.

## Installazione

```console
dotnet add package AnyBase.Net --version 1.4.0
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
`Decimal`, `Hexadecimal`, `Base32`, `Base64` e `Base64Url`. Le factory storiche
continuano a usare `EncodingMode.FixedWidthByte`, il comportamento predefinito
di tutte le versioni 1.x.

Sono supportati anche alfabeti e byte arbitrari:

```csharp
var binary = AnyBaseFactory.Create("01");
var source = new byte[] { 0, 1, 127, 128, 255 };

var symbols = binary.Encode(source);
var roundTrip = binary.DecodeToBytes(symbols);
```

## Modalità packed e RFC 4648

In `EncodingMode.Packed` i byte formano un unico bitstream MSB-first. Gli zero
iniziali sono dati, quindi vengono conservati nel round-trip. L'alfabeto deve
contenere da 2 a 256 simboli e la sua dimensione deve essere una potenza di due.

Le factory RFC sono volutamente distinte da quelle fixed-width:

```csharp
var base16 = AnyBaseFactory.CreateRfc4648Base16();
var base32 = AnyBaseFactory.CreateRfc4648Base32();
var base64 = AnyBaseFactory.CreateRfc4648Base64();
var base64Url = AnyBaseFactory.CreateRfc4648Base64Url(usePadding: false);

base64.EncodeToString("f");       // Zg==
base64Url.EncodeToString("f");    // Zg
base64.DecodeToString("Zg==");    // f
```

Base32 e Base64 includono `=` per impostazione predefinita; il parametro
`usePadding` permette di produrre e accettare la forma senza padding. Base16
non usa padding. Il decoder verifica posizione e quantità del padding e rifiuta
pad bit non nulli, così da accettare soltanto rappresentazioni canoniche.

Per alfabeti custom a potenza di due:

```csharp
var packed = AnyBaseFactory.Create("01", EncodingMode.Packed);
```

I separatori appartengono solo alla modalità fixed-width e non sono ammessi nei
formati packed RFC.

## Buffer, memoria e dimensionamento

Le API basate su span scrivono direttamente in buffer forniti dal chiamante e
restituiscono il numero di elementi prodotti. I metodi di dimensionamento
permettono di allocare una sola volta:

```csharp
ReadOnlySpan<byte> input = new byte[] { 0x00, 0x7F, 0xFF };
var encoded = new char[hexadecimal.GetEncodedLength(input.Length)];
var symbolsWritten = hexadecimal.Encode(input, encoded);

var decoded = new byte[hexadecimal.GetDecodedLength(symbolsWritten)];
var bytesWritten = hexadecimal.Decode(encoded, decoded);
```

In modalità packed `GetDecodedLength(int)` restituisce la dimensione massima
del buffer, perché il solo conteggio non distingue i simboli di padding. Quando
i simboli sono disponibili, `GetDecodedLength(ReadOnlySpan<T>)` restituisce la
dimensione esatta dopo aver validato padding e pad bit.

Sono disponibili anche `EncodeMemory` e `DecodeMemory` per
`ReadOnlyMemory<T>`/`Memory<T>`, oltre a `GetEncodedTextLength` e
`GetMaxEncodedTextLength` per l'output testuale.

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
ad array mantengono sempre i confini; dalla 1.2.0 è possibile anche
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

## Stream sincroni e asincroni

La codifica e la decodifica lavorano incrementalmente, senza caricare l'intero
contenuto in memoria. Gli stream restano aperti:

```csharp
await hexadecimal.EncodeAsync(
    inputStream,
    encodedStream,
    cancellationToken: cancellationToken);

await hexadecimal.DecodeAsync(
    encodedStream,
    outputStream,
    cancellationToken: cancellationToken);
```

Sono disponibili anche le varianti sincrone `Encode` e `Decode`. Il formato
codificato nello stream è testo UTF-8 rigoroso. Il bitstream packed attraversa
correttamente i confini dei buffer; il separatore resta disponibile in modalità
fixed-width.

## Playground WebAssembly

Il [playground Blazor WebAssembly](https://milattanzio.github.io/AnyBase.Net/)
esegue tutto localmente nel browser e permette di:

- scegliere preset fixed-width o RFC 4648, oppure definire un alfabeto custom;
- selezionare visivamente modalità e padding;
- configurare un separatore per la modalità fixed-width;
- validare l'alfabeto in tempo reale con diagnostica dettagliata;
- caricare file e codificare o decodificare i byte senza conversioni implicite;
- passare tra viste testo, esadecimale e byte;
- vedere dimensioni e rapporto di espansione;
- scaricare il risultato;
- condividere preset, alfabeto, separatore, modalità, padding e viste tramite URL.

Per avviarlo in locale:

```console
dotnet run --project AnyBase.Net/AnyBase.Net.Playground
```

## Tool da riga di comando

Installa il .NET global tool:

```console
dotnet tool install --global AnyBase.Net.Tool --version 1.4.0
```

Il comando installato è `anybase`:

```console
anybase encode "Hello" --alphabet hex
# 48656C6C6F

anybase decode 48656C6C6F --alphabet hex
# Hello

anybase encode "A" --alphabet hex --separator "-"
# 4-1

# Formato RFC 4648 interoperabile
anybase encode "foobar" --mode packed --alphabet rfc-base64
# Zm9vYmFy

# Forma URL-safe senza padding
anybase encode "f" --mode packed --alphabet rfc-base64url --padding omit
# Zg

# Codifica un file binario in testo esadecimale senza passare da UTF-8
anybase encode --input photo.png --input-format binary \
  --output photo.anybase --output-format binary --alphabet hex

# Ripristina i byte originali; l'output binario non riceve un newline
anybase decode --input photo.anybase --input-format binary \
  --output photo.restored.png --output-format binary --alphabet hex

# Accetta e produce anche una rappresentazione esadecimale dei byte
anybase encode 000A0DFF --input-format hex --output-format hex --alphabet hex
```

I preset fixed-width sono `binary`, `octal`, `decimal`, `hex`, `base32`,
`base64` e `base64url`; quelli packed sono `rfc-base16`, `rfc-base32`,
`rfc-base64` e `rfc-base64url`. `--mode` accetta `fixed` o `packed`, mentre
`--padding` accetta `include` o `omit`. `--alphabet` accetta anche una sequenza
personalizzata; `--base` mantiene l'alfabeto storico per basi da 2 a 64.
`--input-format` e
`--output-format` accettano `text`, `binary` o `hex`; il formato predefinito è
`text`. Sono inoltre disponibili `--input <file|->`, `--output <file|->` e
stdin/stdout binari diretti. Usa `anybase --help` per la sintassi completa.

Quando l'output è `binary`, la CLI non aggiunge mai un newline. Per questo
formato l'input posizionale non è disponibile: usa un file o stdin.

## Sviluppo

```console
dotnet restore AnyBase.Net/AnyBase.Net.sln
dotnet build AnyBase.Net/AnyBase.Net.sln --configuration Release
dotnet test AnyBase.Net/AnyBase.Net.sln --configuration Release
dotnet pack AnyBase.Net/AnyBase.Net/AnyBase.Net.csproj --configuration Release --output artifacts/packages
dotnet pack AnyBase.Net/AnyBase.Net.Tool/AnyBase.Net.Tool.csproj --configuration Release --output artifacts/packages
# PowerShell 7 (Windows, Linux o macOS)
pwsh ./eng/Test-Packages.ps1 -PackageDirectory artifacts/packages -Version 1.4.0

# Windows PowerShell
powershell -ExecutionPolicy Bypass -File ./eng/Test-Packages.ps1 -PackageDirectory artifacts/packages -Version 1.4.0
```

La suite copre l'intero intervallo dei byte, Unicode UTF-8, alfabeti custom,
input malformati e il contratto della CLI. FsCheck esegue inoltre round-trip
generativi variando byte, ordine e dimensione degli alfabeti e padding.

I benchmark confrontano API ad array e span su payload da 1 KiB e 64 KiB:

```console
dotnet run --project AnyBase.Net/AnyBase.Net.Benchmarks -c Release -- --filter "*"
```

La CI pubblica il report Markdown nel riepilogo del job e conserva l'intera
cartella degli artefatti BenchmarkDotNet per ogni commit.

Le modifiche di ogni versione sono raccolte nel [changelog](CHANGELOG.md).
Le istruzioni per GitHub Pages e Trusted Publishing sono in
[docs/RELEASING.md](docs/RELEASING.md).

## Licenza

[MIT](LICENSE)
