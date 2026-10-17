# TaccuinoRistoranti

Applicazione full-stack ispirata al format 4 Ristoranti con:

- Backend .NET 8 Web API
- Autenticazione JWT con Microsoft ASP.NET Core Identity
- Database PostgreSQL (Neon) con EF Core migrations
- Frontend React + TypeScript
- Mobile Flutter

## Funzionalita principali

- Registrazione e login con JWT
- Refresh token persistente con rinnovo sessione lato client
- Rotazione one-time-use del refresh token con rilevamento riuso token compromesso
- Logout con revoca refresh token
- Creazione di un pasto (pranzo/cena)
- Codice di condivisione del pasto
- Condivisione rapida codice su WhatsApp
- Accesso al pasto tramite codice
- Votazione da 0 a 10 su 5 categorie:
  - Location
  - Menu
  - Servizio
  - Conto
  - Special
- Media per categoria e media generale del pasto
- Classifica finale del pasto ordinata per media del partecipante
- Dettaglio voti per partecipante (location, menu, servizio, conto, special)
- Un utente mantiene un solo voto per pasto ma puo modificarlo in seguito
- Scheda "La tua scheda voto" + classifica finale nascosta ma scopribile

## Struttura progetto

- `backend/TaccuinoRistoranti.Api` API .NET 8 + Identity + EF Core
- `frontend` client React
- `mobile` client Flutter

## Backend

### Configurazione

I segreti del backend non sono versionati nel repository. In locale imposta almeno:

- `backend/TaccuinoRistoranti.Api/appsettings.json`
- `backend/TaccuinoRistoranti.Api/appsettings.Development.json`

oppure, meglio, usa environment variables o `dotnet user-secrets`.

Esempio con user secrets:

```powershell
cd backend/TaccuinoRistoranti.Api

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Port=5432;Database=...;Username=...;Password=..."
dotnet user-secrets set "Jwt:SigningKey" "inserisci-una-chiave-lunga-e-casuale"
```

Le altre impostazioni JWT non sensibili restano nella sezione `Jwt` dei file `appsettings`.

### Comandi utili

```powershell
cd backend/TaccuinoRistoranti.Api

dotnet restore
dotnet build

dotnet ef migrations add NomeMigration
dotnet ef database update

dotnet run
```

Le migration create sono in:

- `backend/TaccuinoRistoranti.Api/Migrations/20261017075634_InitialCreate.cs`
- `backend/TaccuinoRistoranti.Api/Migrations/20261017081048_AddRefreshTokensAndLeaderboard.cs`
- `backend/TaccuinoRistoranti.Api/Migrations/20261017081503_AddRefreshReuseProtection.cs`

## Frontend React

```powershell
cd frontend
npm install
npm run dev
```

API URL di default nel client: `http://localhost:5285`.

Per cambiarlo, crea `frontend/.env`:

```env
VITE_API_URL=http://localhost:5285
```

Logo web: salva l'immagine logo in `frontend/public/logo-4ristoranti.jpg`.

### Pubblicazione su GitHub Pages

Il repository include un workflow in `.github/workflows/deploy-frontend.yml` che pubblica il frontend su GitHub Pages.

Prima del deploy imposta la variabile repository `VITE_API_URL` con l'URL pubblico del backend, ad esempio:

```text
https://<nome-backend>.azurewebsites.net
```

Se vuoi fare una pubblicazione manuale dal workflow, usa `workflow_dispatch` e passa `api_url`.

## Mobile Flutter

```powershell
cd mobile
flutter pub get
flutter run
```

Nel codice mobile l'API e configurata con fallback automatico:

- Android emulator: `http://10.0.2.2:5285`
- Web/iOS/Desktop: `http://localhost:5285`

Override esplicito URL API:

```powershell
flutter run --dart-define API_BASE_URL=http://localhost:5285
```

## Endpoint principali

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET /api/meals`
- `POST /api/meals`
- `POST /api/meals/join`
- `POST /api/meals/{mealId}/ratings`
- `GET /api/meals/{mealId}`

## Docker Compose

Sono inclusi:

- `docker-compose.yml`
- `backend/TaccuinoRistoranti.Api/Dockerfile`
- `frontend/Dockerfile`
- `frontend/nginx.conf`

Avvio stack backend + frontend:

```powershell
docker compose up --build
```

Servizi:

- Frontend: `http://localhost:5173`
- Backend: `http://localhost:8080`

## Bootstrap Locale Unico

Script PowerShell incluso:

- `scripts/bootstrap-local.ps1`

Avvio locale (backend + frontend + setup mobile):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-local.ps1
```

Avvio con Docker Compose:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-local.ps1 -UseDocker
```

## Note

- Migrazione iniziale applicata con successo sul database PostgreSQL Neon fornito.
- Prima di andare in produzione: spostare connection string e chiave JWT su secret manager / variabili ambiente.
