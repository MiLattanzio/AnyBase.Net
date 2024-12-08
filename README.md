# Documentazione della classe Base<TBase>

## Descrizione

La classe `Base<TBase>` rappresenta un sistema generico di codifica e decodifica basato su una tipologia specificata di
base. Fornisce funzionalità per codificare e decodificare dati in un sistema numerico definito da un insieme di valori
di tipo `TBase`.

## Parametri Generici

- `TBase`: Il tipo degli elementi della base. Deve implementare le interfacce `IComparable`, `IComparable<T>`,
  `IConvertible` e `IEquatable<T>`.

## Proprietà

- **Identity** (`IReadOnlyList<TBase>`): Elenco di elementi unici che definiscono l'identità della base utilizzata per
  le operazioni di codifica e decodifica.

- **Size** (`int`): Indica la dimensione utilizzata nelle operazioni di codifica e decodifica all'interno del sistema
  numerico.

- **NumeralSystem** (`NumeralSystem`): Rappresenta il sistema numerico utilizzato per le operazioni di codifica e
  decodifica all'interno della classe `Base`.

## Metodi

### Pubblici

- **Encode(byte[] bytes)**: Codifica un array di byte in un array di `TBase`.

- **Encode(string value)**: Codifica una stringa in un array di `TBase`.

- **EncodeToString(byte[] bytes)**: Codifica un array di byte in una stringa utilizzando il sistema numerico corrente e
  l'identità specificata.

- **EncodeToString(string value)**: Codifica una stringa nel suo formato di rappresentazione codificato.

- **DecodeToString(string encoded)**: Decodifica una stringa codificata nella sua rappresentazione originale.

- **DecodeToString(TBase[] encoded)**: Decodifica un array di elementi di tipo `TBase` in una stringa.

- **DecodeToBytes(TBase[] encoded)**: Decodifica un array di elementi codificati di base `TBase` in un array di byte.

## Costruttore

- `Base(HashSet<TBase> identity)`: Inizializza una nuova istanza della classe `Base` con un'identità specificata.
  L'identità non può essere nulla o vuota e deve contenere elementi unici.

## Note

La classe `Base` è utile in applicazioni che richiedono la conversione tra vari sistemi numerici, come la
rappresentazione binaria, ottale o esadecimale dei dati. La classe fornisce metodi per assicurare la precisione nella
conversione sia di stringhe che di array di byte in diversi formati di rappresentazione numerica.

## Esempio di Utilizzo

Un utilizzo comune della classe `Base` può essere visto nei test unitari della classe `BaseTest`, dove la codifica e la
decodifica tra diversi sistemi numerici sono verificate e validate.

### Esempio di Implementazione della Codifica Esadecimale

Per implementare la codifica esadecimale con la classe `Base`, è necessario definire un'istanza della classe `Base`
utilizzando il set di caratteri che rappresentano i numeri esadecimali:

using System;
using System.Collections.Generic;
using System.Text;

```csharp
namespace AnyBase.Example
{
public class HexEncodingExample
{
public static void Main()
{
// Definire l'identità del sistema numerico esadecimale
var identity = new HashSet<char>(
new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' }
);

            // Creare un'istanza della classe Base usando il set di caratteri esadecimali
            var hexBase = new Base<char>(identity);

            // Stringa da codificare
            var value = "Esempio di testo";

            // Codificare la stringa in esadecimale
            var encodedChars = hexBase.Encode(value);
            var encodedString = hexBase.EncodeToString(value);
            
            // Visualizzare il testo codificato
            Console.WriteLine($"Codificato: {encodedString}");

            // Decodificare l'array di caratteri esadecimali
            var decodedString = hexBase.DecodeToString(encodedChars);

            // Visualizzare il testo decodificato
            Console.WriteLine($"Decodificato: {decodedString}");
        }
    }
}
```

### Descrizione

1. **Definizione dell'Identità**: Creiamo un `HashSet<char>` con i caratteri '0-9' e 'A-F' per rappresentare il sistema
   numerico esadecimale.
2. **Creazione della Classe Base**: Inizializziamo una nuova istanza di `Base<char>` con l'identità del sistema numerico
   esadecimale.
3. **Codifica della Stringa**: Utilizziamo il metodo `EncodeToString` per convertire la stringa originale in una
   rappresentazione esadecimale.
4. **Decodifica della Stringa**: Utilizziamo il metodo `DecodeToString` per convertire l'output esadecimale nuovamente
   nella stringa originale.


