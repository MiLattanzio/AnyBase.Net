# Release di AnyBase.Net

La workflow `.github/workflows/release.yml` crea una release GitHub in bozza,
pubblica `AnyBase.Net` e `AnyBase.Net.Tool` su nuget.org tramite OIDC, allega i
pacchetti e lo ZIP statico del playground, quindi rende pubblica la release.

## Configurazione una tantum

### GitHub Pages

1. Apri **Settings → Pages** nel repository GitHub.
2. In **Build and deployment**, imposta **Source** su **GitHub Actions**.

Il deploy sarà disponibile su <https://milattanzio.github.io/AnyBase.Net/>.

### Trusted Publishing di nuget.org

1. Accedi a <https://www.nuget.org/> con l'account proprietario dei pacchetti
   `AnyBase.Net` e `AnyBase.Net.Tool`.
2. Dal menu del profilo apri **Trusted Publishing** e crea una policy GitHub.
3. Usa questi valori:

   - owner della policy: l'account o l'organizzazione che possiede i pacchetti;
   - repository owner: `MiLattanzio`;
   - repository: `AnyBase.Net`;
   - workflow file: `release.yml` (solo il nome file);
   - environment: lascia vuoto.

4. Nel repository apri **Settings → Secrets and variables → Actions → Variables**.
5. Crea la variabile repository `NUGET_USER` con il nome profilo nuget.org
   (non l'indirizzo email).

La policy è valida per i pacchetti posseduti dallo stesso owner e non richiede
una API key permanente.

## Verifiche eseguite dalla CI

La CI compila e testa il progetto su Windows e Linux. Durante `dotnet pack`, la
validazione pacchetti dell'SDK .NET confronta l'API pubblica con la versione
indicata da `PackageValidationBaselineVersion` nel progetto della libreria.

Dopo il pack, `eng/Test-Packages.ps1` crea un'applicazione consumer usando
esclusivamente il nuovo pacchetto locale, installa il global tool in una cartella
temporanea e ne verifica l'esecuzione.

## Pubblicare una versione

1. Aggiorna `Version`, `AssemblyVersion` e `FileVersion` in
   `Directory.Build.props`.
2. Aggiorna `PackageValidationBaselineVersion` all'ultima versione stabile
   compatibile e completa data e note in `CHANGELOG.md`.
3. Esegui le verifiche locali:

   ```console
   dotnet restore AnyBase.Net/AnyBase.Net.sln
   dotnet build AnyBase.Net/AnyBase.Net.sln --configuration Release --no-restore
   dotnet test AnyBase.Net/AnyBase.Net.sln --configuration Release --no-build
   dotnet pack AnyBase.Net/AnyBase.Net/AnyBase.Net.csproj --configuration Release --no-build --output artifacts/packages
   dotnet pack AnyBase.Net/AnyBase.Net.Tool/AnyBase.Net.Tool.csproj --configuration Release --no-build --output artifacts/packages
   # PowerShell 7 (Windows, Linux o macOS)
   pwsh ./eng/Test-Packages.ps1 -PackageDirectory artifacts/packages -Version X.Y.Z

   # Windows PowerShell
   powershell -ExecutionPolicy Bypass -File ./eng/Test-Packages.ps1 -PackageDirectory artifacts/packages -Version X.Y.Z
   ```

4. Integra le modifiche in `master` e attendi che CI e Pages siano verdi.
5. Crea e pubblica il tag corrispondente:

   ```console
   git tag -a vX.Y.Z -m "AnyBase.Net X.Y.Z"
   git push origin vX.Y.Z
   ```

La workflow rifiuta un tag che non corrisponde esattamente alla versione del
progetto. Può essere rilanciata manualmente indicando un tag esistente.
