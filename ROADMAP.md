# PasswordVault – Roadmap
> Last updated: 2026-02-23

# Known Issues

- [ ] Filters are reset after editing a Password.
- [ ] Need a quicker way to edit categories.


# Notes 

> This is a work in progress. 
> I will add more features as I go.
> This is a cross-platform application.
> This is a free and open source application.
> This is a password manager.

---

## Milestone 1 — Import/Export *current*
> **Goal:** Let users move data in and out of PasswordVault.

- [x] `ImportExportService` — CSV export (Title, Username, Password, URL, Notes, Category, Tags)
- [x] Auto-detect CSV format by headers
- [x] Security warning dialog before plain-text export

--- 
<!-- AI generated features / TODO Change later -->

## Milestone 2 — Quick Security Wins
> **Goal:** Low-effort features that significantly improve security posture.

- [ ] **Auto-lock timer** — enforce the existing `AutoLockTimeMinutes` with a `DispatcherTimer`
- [ ] **Clipboard auto-clear** — clear clipboard 30s after copying a password
- [ ] **Master password change UI** — button in Settings, backend already exists in `AuthService`

---

## Milestone 3 — Vault Health & Awareness
> **Goal:** Help users understand the security state of their vault.

- [ ] **Breach detection (HIBP)** — check passwords via k-anonymity API, flag compromised ones
- [ ] **Duplicate password detection** — compare encrypted hashes, warn on reuse
- [ ] **Password age warnings** — flag passwords not changed in 90+ days
- [ ] **Vault health report** — aggregate score: weak + reused + old + breached

---

## Milestone 4 — UX Polish
> **Goal:** Small features that make daily use smoother.

- [ ] **Trash / soft-delete recovery** — UI to view & restore `IsDeleted` entries
- [ ] **Sorting options** — sort password list by name, date, strength, category
- [ ] **Keyboard shortcuts** — Ctrl+N (new), Ctrl+F (search), Ctrl+L (lock)
- [ ] **Favicon fetching** — show website icons next to entries

---

## Milestone 5 — Advanced Features
> **Goal:** Features that set PasswordVault apart.

- [ ] **TOTP / 2FA codes** — store TOTP secrets, generate live 6-digit codes
- [ ] **Secure notes** — standalone encrypted notes (not tied to a password entry)
- [ ] **Custom fields** — key-value pairs per entry (security questions, PINs, etc.)
- [ ] **Biometric unlock** — Windows Hello integration (stubs already exist)

---

## Backlog / Long-Term
> Features that require significant architecture or external infrastructure.

- [ ] Browser extension (auto-fill)
- [ ] Cross-device sync (cloud-based, replace LAN sync)
- [ ] Shared vaults / password sharing
- [ ] Multiple vaults (personal/work)
- [ ] Emergency access (trusted contacts)
- [ ] Dark web monitoring (paid API)
- [ ] Attachments (encrypted file storage)
- [ ] Nested categories / folders
- [ ] Password expiry reminders / notifications

---

## Completed ✅

- [x] Master password auth (Argon2id + AES-256-GCM)
- [x] Password CRUD (title, username, password, URL, notes)
- [x] Categories with custom color + icon
- [x] Category management (add/edit/delete)
- [x] Password generator (random + memorable)
- [x] Password strength evaluator
- [x] Search & filter (text, category, tags, favorites, strength)
- [x] Dashboard with stats
- [x] Favorites system
- [x] Tags system
- [x] Dark/light/system theme
- [x] Database backup/restore
- [x] Basic test scaffold (xUnit + NSubstitute)
