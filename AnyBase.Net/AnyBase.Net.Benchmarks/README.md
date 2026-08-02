# AnyBase.Net benchmarks

I benchmark confrontano le API che restituiscono array con le API basate su
buffer `Span<T>` forniti dal chiamante. Entrambi i gruppi usano dati
deterministici da 1 KiB e 64 KiB e pubblicano tempi, rapporto rispetto alla
baseline e allocazioni.

Esecuzione locale:

```console
dotnet run --project AnyBase.Net/AnyBase.Net.Benchmarks -c Release -- --filter "*"
```

La CI esegue i benchmark su Linux, aggiunge il report Markdown al riepilogo del
job e carica l'intera directory BenchmarkDotNet come artifact
`benchmark-baseline-<commit>`.
