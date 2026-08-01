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

1. Accedi a <https://www.nuget.org/> con l'account proprietario del pacchetto
   `AnyBase.Net`.
2. Dal menu del profilo apri **Trusted Publishing** e crea una policy GitHub.
3. Usa questi valori:

   - owner della policy: l'account o l'organizzazione che possiede il pacchetto;
   - repository owner: `MiLattanzio`;
   - repository: `AnyBase.Net`;
   - workflow file: `release.yml` (solo il nome file);
   - environment: lascia vuoto.

4. Nel repository apri **Settings → Secrets and variables → Actions → Variables**.
5. Crea la variabile repository `NUGET_USER` con il nome profilo nuget.org
   (non l'indirizzo email).

La policy è valida per i pacchetti posseduti dallo stesso owner e non richiede
una API key permanente.

## Pubblicare una versione

1. Aggiorna `Version`, `AssemblyVersion` e `FileVersion` in
   `Directory.Build.props`.
2. Esegui build, test e pack in locale.
3. Integra le modifiche in `master` e attendi che CI e Pages siano verdi.
4. Crea e pubblica il tag corrispondente:

   ```console
   git tag v1.1.0
   git push origin v1.1.0
   ```

La workflow rifiuta un tag che non corrisponde esattamente alla versione del
progetto. Può essere rilanciata manualmente indicando un tag esistente.
