import axios from "axios";
import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import "./App.css";
import fallbackLogo from "./assets/hero.png";

type AuthMode = "login" | "register";
type MealType = 0 | 1;

type RatingInput = {
  location: number;
  menu: number;
  service: number;
  bill: number;
  special: number;
};

type RatingSummary = {
  location: number;
  menu: number;
  service: number;
  bill: number;
  special: number;
  overall: number;
  votes: number;
};

type ParticipantScore = {
  userId: string;
  userEmail: string;
  location: number;
  menu: number;
  service: number;
  bill: number;
  special: number;
  overall: number;
  position: number;
  createdAtUtc: string;
};

type UserRating = {
  location: number;
  menu: number;
  service: number;
  bill: number;
  special: number;
  createdAtUtc: string;
};

type Meal = {
  id: string;
  name: string;
  street: string;
  city: string;
  notes: string | null;
  mealType: MealType;
  shareCode: string;
  createdAtUtc: string;
  createdBy: string;
  hasUserRated: boolean;
  userRating: UserRating | null;
  summary: RatingSummary;
  leaderboard: ParticipantScore[];
};

type AuthResponse = {
  token: string;
  refreshToken: string;
  email: string;
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
};

const apiBaseUrl = import.meta.env.VITE_API_URL ?? "http://localhost:5285";

const api = axios.create({
  baseURL: apiBaseUrl,
});

const emptyRating: RatingInput = {
  location: 0,
  menu: 0,
  service: 0,
  bill: 0,
  special: 0,
};

const ratingFields: Array<keyof RatingInput> = [
  "location",
  "menu",
  "service",
  "bill",
  "special",
];

const ratingFieldLabels: Record<keyof RatingInput, string> = {
  location: "Location",
  menu: "Menu",
  service: "Servizio",
  bill: "Conto",
  special: "Special",
};

const storage = {
  token: "tr_token",
  refreshToken: "tr_refresh_token",
  accessExpiresAt: "tr_access_expires_at",
  refreshExpiresAt: "tr_refresh_expires_at",
};

const ratingFromMeal = (meal: Meal | null): RatingInput => {
  if (!meal?.userRating) {
    return emptyRating;
  }

  return {
    location: meal.userRating.location,
    menu: meal.userRating.menu,
    service: meal.userRating.service,
    bill: meal.userRating.bill,
    special: meal.userRating.special,
  };
};

function App() {
  const [token, setToken] = useState<string | null>(
    localStorage.getItem(storage.token),
  );
  const [refreshToken, setRefreshToken] = useState<string | null>(
    localStorage.getItem(storage.refreshToken),
  );
  const [accessExpiresAt, setAccessExpiresAt] = useState<string | null>(
    localStorage.getItem(storage.accessExpiresAt),
  );
  const [refreshExpiresAt, setRefreshExpiresAt] = useState<string | null>(
    localStorage.getItem(storage.refreshExpiresAt),
  );

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [authMode, setAuthMode] = useState<AuthMode>("login");
  const [meals, setMeals] = useState<Meal[]>([]);
  const [joinCode, setJoinCode] = useState("");
  const [selectedMeal, setSelectedMeal] = useState<Meal | null>(null);
  const [newMeal, setNewMeal] = useState({
    name: "",
    street: "",
    city: "",
    notes: "",
    mealType: 0 as MealType,
  });
  const [rating, setRating] = useState<RatingInput>(emptyRating);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  const accessExpiryDate = useMemo(
    () => (accessExpiresAt ? new Date(accessExpiresAt) : null),
    [accessExpiresAt],
  );

  const isExpired = (isoDate: string | null) => {
    if (!isoDate) {
      return true;
    }

    return new Date(isoDate).getTime() <= Date.now();
  };

  const applySession = (auth: AuthResponse) => {
    localStorage.setItem(storage.token, auth.token);
    localStorage.setItem(storage.refreshToken, auth.refreshToken);
    localStorage.setItem(storage.accessExpiresAt, auth.accessTokenExpiresAtUtc);
    localStorage.setItem(
      storage.refreshExpiresAt,
      auth.refreshTokenExpiresAtUtc,
    );

    setToken(auth.token);
    setRefreshToken(auth.refreshToken);
    setAccessExpiresAt(auth.accessTokenExpiresAtUtc);
    setRefreshExpiresAt(auth.refreshTokenExpiresAtUtc);
  };

  const clearSession = () => {
    localStorage.removeItem(storage.token);
    localStorage.removeItem(storage.refreshToken);
    localStorage.removeItem(storage.accessExpiresAt);
    localStorage.removeItem(storage.refreshExpiresAt);
    setToken(null);
    setRefreshToken(null);
    setAccessExpiresAt(null);
    setRefreshExpiresAt(null);
    setMeals([]);
    setSelectedMeal(null);
    setRating(emptyRating);
  };

  const refreshSession = async () => {
    if (!refreshToken || isExpired(refreshExpiresAt)) {
      clearSession();
      return false;
    }

    try {
      const response = await api.post<AuthResponse>("/api/auth/refresh", {
        refreshToken,
      });
      applySession(response.data);
      return true;
    } catch {
      clearSession();
      return false;
    }
  };

  const ensureAccessToken = async () => {
    if (!token) {
      return false;
    }

    if (!isExpired(accessExpiresAt)) {
      return true;
    }

    return await refreshSession();
  };

  const getAuthHeader = async () => {
    const valid = await ensureAccessToken();
    if (!valid) {
      return null;
    }

    const activeToken = localStorage.getItem(storage.token);
    return activeToken ? { Authorization: `Bearer ${activeToken}` } : null;
  };

  const syncSelectedMeal = (incomingMeals: Meal[]) => {
    if (!selectedMeal) {
      return;
    }

    const maybeUpdated = incomingMeals.find((m) => m.id === selectedMeal.id);
    setSelectedMeal(maybeUpdated ?? null);
    setRating(ratingFromMeal(maybeUpdated ?? null));
  };

  useEffect(() => {
    if (!token) {
      setMeals([]);
      setSelectedMeal(null);
      setRating(emptyRating);
      return;
    }

    void loadMeals();
  }, [token]);

  const loadMeals = async () => {
    const authHeader = await getAuthHeader();
    if (!authHeader) {
      return;
    }

    try {
      const response = await api.get<Meal[]>("/api/meals", {
        headers: authHeader,
      });
      setMeals(response.data);
      syncSelectedMeal(response.data);
    } catch {
      setError("Impossibile caricare i pasti.");
    }
  };

  const handleAuth = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError("");

    try {
      const endpoint =
        authMode === "login" ? "/api/auth/login" : "/api/auth/register";
      const response = await api.post<AuthResponse>(endpoint, {
        email,
        password,
      });
      applySession(response.data);
      setEmail("");
      setPassword("");
    } catch (error) {
      if (axios.isAxiosError(error)) {
        const responseData = error.response?.data;

        if (typeof responseData === "string") {
          setError(responseData);
        } else if (Array.isArray(responseData)) {
          setError(responseData.join(" "));
        } else if (responseData && typeof responseData === "object") {
          const values = Object.values(responseData as Record<string, unknown>);
          setError(
            values
              .flatMap((value) => (Array.isArray(value) ? value : [value]))
              .map(String)
              .join(" "),
          );
        } else {
          setError(
            "Autenticazione fallita. Verifica credenziali o password minima.",
          );
        }
      } else {
        setError(
          "Autenticazione fallita. Verifica credenziali o password minima.",
        );
      }
    } finally {
      setBusy(false);
    }
  };

  const logout = async () => {
    try {
      if (refreshToken) {
        const authHeader = token
          ? { Authorization: `Bearer ${token}` }
          : undefined;
        await api.post(
          "/api/auth/logout",
          { refreshToken },
          { headers: authHeader },
        );
      }
    } catch {
      // ignore
    } finally {
      clearSession();
    }
  };

  const createMeal = async (event: FormEvent) => {
    event.preventDefault();

    setBusy(true);
    setError("");
    try {
      const authHeader = await getAuthHeader();
      if (!authHeader) {
        return;
      }

      const response = await api.post<Meal>("/api/meals", newMeal, {
        headers: authHeader,
      });
      setMeals((current) => [response.data, ...current]);
      setSelectedMeal(response.data);
      setRating(ratingFromMeal(response.data));
      setNewMeal({ name: "", street: "", city: "", notes: "", mealType: 0 });
    } catch {
      setError("Creazione pasto fallita.");
    } finally {
      setBusy(false);
    }
  };

  const joinMeal = async (event: FormEvent) => {
    event.preventDefault();

    setBusy(true);
    setError("");
    try {
      const authHeader = await getAuthHeader();
      if (!authHeader) {
        return;
      }

      const response = await api.post<Meal>(
        "/api/meals/join",
        { shareCode: joinCode },
        { headers: authHeader },
      );
      setSelectedMeal(response.data);
      setRating(ratingFromMeal(response.data));
      setJoinCode("");
      await loadMeals();
    } catch {
      setError("Codice pasto non valido.");
    } finally {
      setBusy(false);
    }
  };

  const submitRating = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedMeal) {
      return;
    }

    setBusy(true);
    setError("");
    try {
      const authHeader = await getAuthHeader();
      if (!authHeader) {
        return;
      }

      const response = await api.post<Meal>(
        `/api/meals/${selectedMeal.id}/ratings`,
        rating,
        {
          headers: authHeader,
        },
      );
      setSelectedMeal(response.data);
      setRating(ratingFromMeal(response.data));
      await loadMeals();
    } catch {
      setError("Votazione non valida.");
    } finally {
      setBusy(false);
    }
  };

  const selectMeal = (meal: Meal) => {
    setSelectedMeal(meal);
    setRating(ratingFromMeal(meal));
  };

  const shareOnWhatsapp = (meal: Meal) => {
    const text = encodeURIComponent(
      `Partecipa a ${meal.name} su Taccuino 4 Ristoranti. Codice: ${meal.shareCode}.`,
    );
    window.open(`https://wa.me/?text=${text}`, "_blank", "noopener,noreferrer");
  };

  const sessionLabel = accessExpiryDate
    ? `Sessione valida fino a ${accessExpiryDate.toLocaleTimeString("it-IT")}`
    : "Sessione non attiva";

  const userRatingDate = selectedMeal?.userRating?.createdAtUtc
    ? new Date(selectedMeal.userRating.createdAtUtc).toLocaleString("it-IT")
    : null;

  const brandLogoUrl = `${import.meta.env.BASE_URL}logo-4ristoranti.jpg`;

  return (
    <div className="page">
      <header className="hero">
        <div className="brand-mark">
          <img
            src={brandLogoUrl}
            alt="Logo 4 Ristoranti"
            className="brand-logo"
            onError={(event) => {
              event.currentTarget.src = fallbackLogo;
            }}
          />
        </div>
        <div className="hero-copy">
          <p className="eyebrow">Taccuino 4 Ristoranti</p>
          <h1>Assegna il tuo voto e ribalta il risultato!</h1>
          <p>
            Condividi il codice del pasto su WhatsApp, compila la tua scheda
            personale e aggiorna la votazione quando vuoi.
          </p>
        </div>
        <div className="api-badge">API: {apiBaseUrl}</div>
      </header>

      {token ? <p className="session-status">{sessionLabel}</p> : null}

      {!token ? (
        <section className="card">
          <h2>{authMode === "login" ? "Accedi" : "Registrati"}</h2>
          <form onSubmit={handleAuth} className="grid">
            <label>
              Email
              <input
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                type="email"
                required
              />
            </label>

            <label>
              Password
              <input
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                type="password"
                required
              />
            </label>

            <button type="submit" disabled={busy}>
              {busy
                ? "Attendi..."
                : authMode === "login"
                  ? "Login"
                  : "Crea account"}
            </button>
          </form>

          <button
            className="link"
            onClick={() =>
              setAuthMode((mode) => (mode === "login" ? "register" : "login"))
            }
          >
            {authMode === "login"
              ? "Non hai un account? Registrati"
              : "Hai gia un account? Effettua login"}
          </button>
        </section>
      ) : (
        <>
          <section className="card actions">
            <h2>Crea pasto</h2>
            <form onSubmit={createMeal} className="grid two">
              <label>
                Nome locale
                <input
                  value={newMeal.name}
                  onChange={(event) =>
                    setNewMeal({ ...newMeal, name: event.target.value })
                  }
                  required
                />
              </label>
              <label>
                Via
                <input
                  value={newMeal.street}
                  onChange={(event) =>
                    setNewMeal({ ...newMeal, street: event.target.value })
                  }
                  required
                />
              </label>
              <label>
                Citta
                <input
                  value={newMeal.city}
                  onChange={(event) =>
                    setNewMeal({ ...newMeal, city: event.target.value })
                  }
                  required
                />
              </label>
              <label>
                Tipo pasto
                <select
                  value={newMeal.mealType}
                  onChange={(event) =>
                    setNewMeal({
                      ...newMeal,
                      mealType: Number(event.target.value) as MealType,
                    })
                  }
                >
                  <option value={0}>Pranzo</option>
                  <option value={1}>Cena</option>
                </select>
              </label>
              <label className="span2">
                Note
                <textarea
                  value={newMeal.notes}
                  onChange={(event) =>
                    setNewMeal({ ...newMeal, notes: event.target.value })
                  }
                  rows={3}
                />
              </label>
              <button type="submit" disabled={busy} className="span2">
                Crea pasto
              </button>
            </form>
          </section>

          <section className="card">
            <h2>Partecipa con codice</h2>
            <form onSubmit={joinMeal} className="join-row">
              <input
                value={joinCode}
                onChange={(event) =>
                  setJoinCode(event.target.value.toUpperCase())
                }
                maxLength={6}
                placeholder="Es. A1B2C3"
              />
              <button type="submit" disabled={busy}>
                Apri pasto
              </button>
              <button type="button" onClick={logout} className="secondary">
                Logout
              </button>
            </form>
          </section>

          <section className="layout">
            <article className="card">
              <h2>I tuoi pasti</h2>
              <div className="meal-list">
                {meals.length === 0 ? <p>Nessun pasto disponibile.</p> : null}
                {meals.map((meal) => (
                  <button
                    key={meal.id}
                    className="meal-item"
                    onClick={() => selectMeal(meal)}
                  >
                    <strong>{meal.name}</strong>
                    <span>{meal.city}</span>
                    <span>
                      Codice <b>{meal.shareCode}</b>
                    </span>
                    <span>Media: {meal.summary.overall.toFixed(2)}</span>
                    <span>Votanti: {meal.summary.votes}</span>
                  </button>
                ))}
              </div>
            </article>

            <article className="card">
              <h2>Dettaglio e votazione</h2>
              {!selectedMeal ? (
                <p>Seleziona o apri un pasto con codice.</p>
              ) : null}
              {selectedMeal ? (
                <>
                  <p>
                    <strong>{selectedMeal.name}</strong> - {selectedMeal.street}
                    , {selectedMeal.city}
                  </p>
                  <p>
                    Tipo: {selectedMeal.mealType === 0 ? "Pranzo" : "Cena"} |
                    Codice: <b>{selectedMeal.shareCode}</b>
                  </p>

                  <div className="share-row">
                    <button
                      type="button"
                      className="secondary"
                      onClick={() => shareOnWhatsapp(selectedMeal)}
                    >
                      Condividi codice su WhatsApp
                    </button>
                  </div>

                  <section className="vote-card">
                    <h3>La tua scheda voto</h3>
                    {selectedMeal.userRating ? (
                      <p className="muted">Ultimo invio: {userRatingDate}</p>
                    ) : (
                      <p className="muted">
                        Non hai ancora votato questo pasto.
                      </p>
                    )}

                    <div className="my-score-grid">
                      {ratingFields.map((field) => (
                        <div key={field} className="my-score-item">
                          <span>{ratingFieldLabels[field]}</span>
                          <strong>{rating[field]}</strong>
                        </div>
                      ))}
                    </div>

                    <form onSubmit={submitRating} className="grid two">
                      {ratingFields.map((field) => (
                        <label key={field}>
                          {ratingFieldLabels[field]}
                          <input
                            type="number"
                            min={0}
                            max={10}
                            value={rating[field]}
                            onChange={(event) =>
                              setRating((current) => ({
                                ...current,
                                [field]: Number(event.target.value),
                              }))
                            }
                            required
                          />
                        </label>
                      ))}
                      <button type="submit" className="span2" disabled={busy}>
                        {selectedMeal.hasUserRated
                          ? "Aggiorna il tuo voto"
                          : "Invia voto"}
                      </button>
                    </form>
                  </section>

                  <details className="secret-board">
                    <summary>
                      Scopri classifica finale ({selectedMeal.summary.votes}{" "}
                      votanti)
                    </summary>
                    <p className="muted">
                      Media complessiva:{" "}
                      {selectedMeal.summary.overall.toFixed(2)}
                    </p>
                    <table className="leaderboard-table">
                      <thead>
                        <tr>
                          <th>#</th>
                          <th>Partecipante</th>
                          <th>Location</th>
                          <th>Menu</th>
                          <th>Servizio</th>
                          <th>Conto</th>
                          <th>Special</th>
                          <th>Media</th>
                        </tr>
                      </thead>
                      <tbody>
                        {selectedMeal.leaderboard.map((entry) => (
                          <tr key={entry.userId}>
                            <td>{entry.position}</td>
                            <td>{entry.userEmail}</td>
                            <td>{entry.location}</td>
                            <td>{entry.menu}</td>
                            <td>{entry.service}</td>
                            <td>{entry.bill}</td>
                            <td>{entry.special}</td>
                            <td>{entry.overall.toFixed(2)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </details>
                </>
              ) : null}
            </article>
          </section>
        </>
      )}

      {error ? <p className="error">{error}</p> : null}
    </div>
  );
}

export default App;
