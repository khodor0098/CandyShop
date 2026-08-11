# Candy Van Management System

A small, single-user web application for running sales and the product catalogue of a
candy/sweets van. Built with ASP.NET Core MVC (.NET 9), Entity Framework Core and SQLite.

Three working pages plus category management and printable invoices:

| Page | Route | Purpose |
| --- | --- | --- |
| **Sales** (default) | `/Sales` | Pick products, adjust quantities, complete a sale, print the invoice |
| **Invoice** | `/Sales/Invoice/{id}` | Print-friendly receipt for a saved sale |
| **Products** | `/Products` | Add/edit products, set price and category, activate/deactivate |
| **Categories** | `/Categories` | Add/edit categories, activate/deactivate |
| **Reports** | `/Reports` | Totals and sales list with date/category/product/sale-ID filters |
| **Sale details** | `/Reports/Sale/{id}` | Line-by-line breakdown of one sale |
| **Login** | `/Account/Login` | The only anonymous page |

---

## 1. Quick start

```bash
dotnet restore
```

```bash
dotnet tool restore
```

```bash
dotnet run
```

Then open the URL printed in the console (`http://localhost:5000` by default) and sign in.

**Default admin credentials** (from `appsettings.json`):

| Field | Value |
| --- | --- |
| Username | `admin` |
| Password | `ChangeMe123` |

> Change the password before using this anywhere real — see [Security](#7-security).

The database file (`candyshop.db`) is created automatically on first run, migrations are
applied, and five sample products in four categories are seeded. No manual database step is
required.

### Running on a tablet/phone in the van

Kestrel binds to localhost only by default. To reach the app from another device on the same
network, bind to all interfaces:

```bash
dotnet run --urls http://0.0.0.0:5000
```

Then browse to `http://<laptop-ip>:5000`. The UI is responsive and works down to phone width.

---

## 2. Architecture

Plain ASP.NET Core MVC — no extra layers, no repositories, no API project, no third-party
packages beyond EF Core itself.

```
Program.cs                     Startup: DI, cookie auth, rate limiter, routing, auto-migrate
/Controllers
    AccountController.cs       Login / Logout / AccessDenied
    SalesController.cs         Sales page, sale persistence, invoice
    ProductsController.cs      Product list + create/edit/activate
    CategoriesController.cs    Category list + create/edit/activate
    ReportsController.cs       Filtered reports + sale details
    HomeController.cs          "/" redirect to Sales, friendly error page
/Models                        Category, Product, Sale, SaleItem, ErrorViewModel
/Data
    ApplicationDbContext.cs    Mapping, value converters, relationships
    DbInitializer.cs           Migrate on startup + seed when empty
/ViewModels                    One view model per screen (entities are not passed to views)
/Security
    AdminCredentialsOptions.cs Bound from configuration
    AdminAuthenticator.cs      Validates the single login
    PasswordHasher.cs          PBKDF2-SHA256 hash/verify
/Configuration
    StoreOptions.cs            Store name / footer printed on invoices
/Views                         Razor views (Account, Sales, Products, Categories, Reports, Shared)
/wwwroot
    css/site.css               Layout, invoice and @media print rules
    js/sales.js                Cart, category filter, product search
    js/site.js                 data-confirm confirmation dialogs
    lib/                       Bootstrap 5 + jQuery, served locally (no CDN, works offline)
/Migrations                    InitialCreate, AddCategoriesAndInvoiceSupport
```

Requests follow the standard MVC flow, and every state change uses POST → redirect → GET
(so a refresh never re-submits a sale).

---

## 3. Database structure

```
Category 1 ──── many Product 1 ──── many SaleItem many ──── 1 Sale
```

**Category** — `Id`, `Name` (unique), `IsActive`, `CreatedAt`, `UpdatedAt`

**Product** — `Id`, `Name`, `CategoryId` → Category, `Price`, `IsActive`, `CreatedAt`, `UpdatedAt`

**Sale** — `Id`, `SaleDate`, `Total`

**SaleItem** — `Id`, `SaleId` → Sale, `ProductId` → Product, `ProductName`, `UnitPrice`,
`Quantity`, `Total`

### Why SaleItem duplicates the product name and price

`ProductName` and `UnitPrice` are **snapshots taken at the moment of sale**. Renaming a
product or changing its price never alters a completed sale. Verified behaviour: a product
sold at `$1.50` still shows `$1.50` on its invoice after the price is changed to `$4.44`.

The product's **category** is deliberately *not* snapshotted — reports show the product's
current category, so re-organising the catalogue re-organises historical reporting too. If
you need frozen categories per line, add a `CategoryName` column to `SaleItem` and populate
it in `SalesController.Complete`.

### Foreign keys and delete behaviour

| Relationship | Behaviour | Effect |
| --- | --- | --- |
| Sale → SaleItem | `Cascade` | Deleting a sale removes its lines |
| Product → SaleItem | `Restrict` | A product that has been sold cannot be deleted |
| Category → Product | `Restrict` | A category holding products cannot be deleted |

The UI never deletes products or categories at all — it toggles `IsActive`.

### Money is stored as integer cents

SQLite has no decimal type; its options are `REAL` (lossy binary floating point) or `TEXT`
(sorts as a string). The models use `decimal`, and a single EF value converter in
`ApplicationDbContext` stores every money column as an **`INTEGER` number of cents**. That is
exact and still sorts and compares correctly in SQL.

Consequence: SQL-side `SUM()` cannot be translated over a converted column, so report totals
are summed in memory over the filtered rows. That is intentional and appropriate at this
scale (one van); the report also caps at 500 rows and tells you when the cap is hit.

### Timestamps

All timestamps are stored in **UTC** and displayed in the server's local time. Report date
pickers are treated as local days and converted to UTC boundaries before querying. A value
converter re-applies `DateTimeKind.Utc` on read, because SQLite otherwise returns
`DateTimeKind.Unspecified` — which would silently break the local-time conversion.

---

## 4. Database commands

Startup already does this for you (`DbInitializer.InitializeAsync` calls
`Database.MigrateAsync()`, then seeds only if the `Products` table is empty). The explicit
commands are:

```bash
dotnet tool restore
```

```bash
dotnet dotnet-ef migrations add InitialCreate
```

```bash
dotnet dotnet-ef database update
```

```bash
dotnet run
```

`dotnet-ef` is pinned as a **local** tool in `.config/dotnet-tools.json` (version 9.x, matching
EF Core), so run it as `dotnet dotnet-ef …`. A globally installed `dotnet-ef` 10.x requires a
.NET 10 runtime and will not work with this project.

### Automatic migration on startup — how it works

`Program.cs` calls `DbInitializer.InitializeAsync` after the host is built:

1. `Database.MigrateAsync()` creates `candyshop.db` if it is missing and applies any
   migration recorded in `/Migrations` that is not yet in the `__EFMigrationsHistory` table.
   Already-applied migrations are skipped, so restarts are cheap and safe.
2. If `Products` is empty (and `Database:SeedSampleProducts` is `true`), the starter
   categories and products are inserted. An existing database is never re-seeded.

To start over, stop the app, delete `candyshop.db` and run again.

To disable seeding, set `"Database": { "SeedSampleProducts": false }`. To change what is
seeded, edit the `SeedData` array in `Data/DbInitializer.cs`.

### Note on the AddCategories migration

`AddCategoriesAndInvoiceSupport` creates the `Categories` table, inserts a fallback
`Uncategorized` category **only if products already exist**, backfills every existing product
onto it, and *then* adds the foreign key. Order matters: adding the constraint first would
leave pre-existing rows pointing at a non-existent `CategoryId 0`. A fresh database skips the
fallback category entirely.

EF logs a warning that SQLite's `PRAGMA foreign_keys = 0` (used for the table rebuild that
adds the foreign key) cannot run inside a transaction. This is normal for SQLite schema
changes. Back up `candyshop.db` before upgrading a database that holds real sales.

---

## 5. Sales and invoicing

### Completing a sale

The cart lives in the browser (JavaScript + `localStorage`, so a refresh does not lose it)
purely for speed. It is a display aid only. On submit the browser posts **product ids and
quantities and nothing else**; `SalesController.Complete` then:

1. Collapses duplicate lines for the same product.
2. Rejects an empty cart and any quantity outside 1–1000.
3. Loads each product from the database and re-reads its current price.
4. Rejects the sale if a product has become unsellable since the page loaded.
5. Computes every line total and the sale total server-side.
6. Writes `Sale` + `SaleItems` inside a **database transaction**.
7. Stamps `SaleDate` with `DateTime.UtcNow`.
8. Redirects to the invoice for the saved sale.

Prices, line totals and the grand total submitted by the browser are ignored — injecting
`UnitPrice`/`Total` fields into the form has no effect on what is stored.

### Invoice / receipt

The sale is committed **before** the invoice is rendered, so every invoice corresponds to a
real, saved sale. `/Sales/Invoice/{id}` reads it back from the database and shows the store
name, invoice number, date/time, each line (product, quantity, unit price, line total), the
grand total and a closing message. Invoices stay reachable later from **Reports → sale →
Invoice**.

**Print Invoice** calls the browser's `window.print()`. The `@media print` rules in
`wwwroot/css/site.css` hide the navigation, footer, alerts, buttons and every element marked
`.no-print`, so only the receipt is printed. The receipt is a single narrow monospaced column
(`max-width: 76mm`, `@page { size: auto }`), which fills an 80 mm thermal roll and prints as a
receipt-shaped column on A4.

Customise the printed header and footer in `appsettings.json`:

```json
"Store": {
  "Name": "CANDY VAN",
  "Subtitle": "",
  "FooterMessage": "Thank you!"
}
```

---

## 6. Categories

Every product belongs to exactly one category. Categories are managed at `/Categories` and
are never deleted, only deactivated.

- Only **active** categories can be assigned to a new product. When editing a product whose
  category has since been deactivated, that category stays selectable so the value is not
  lost silently.
- A product appears on the Sales page only when **both the product and its category are
  active**. Deactivating a category therefore hides all of its products; the confirmation
  dialog says how many are affected.
- The Sales page groups products under category headings and offers one-tap category pills
  plus a text search; the two filters combine.
- Reports include a **Category** filter and a Categories column, and choosing a category
  narrows the product dropdown to that category. With a category or product filter applied,
  the totals cover the matching lines only, not whole sales.

---

## 7. Security

- Cookie authentication; a global `AuthorizeFilter` means **every** page requires a login and
  only `Login`/`AccessDenied`/`Error` are `[AllowAnonymous]`.
- All POST actions use `[ValidateAntiForgeryToken]` (verified: an authenticated POST without a
  token gets HTTP 400).
- No user table: the admin password is only ever read from configuration, never written to the
  database.
- Login failures return one generic message and are compared in constant time. The login POST
  is rate-limited to 10 attempts per minute.
- Server-side validation on every form; the browser is never trusted for prices or totals.
- `Url.IsLocalUrl` guards the login `returnUrl` against open redirects.
- Errors show a friendly page with a trace reference; stack traces go to the log only.

### Setting a production password

Use a PBKDF2 hash instead of a plain-text password. Generate one:

```bash
dotnet run -- hash-password "YourStrongPassword"
```

Put the output in `AdminCredentials:PasswordHash` and leave `Password` empty — the hash takes
precedence when both are present:

```json
"AdminCredentials": {
  "Username": "admin",
  "Password": "",
  "PasswordHash": "PBKDF2$210000$…$…"
}
```

Better still, keep it out of the repository entirely — user secrets in development:

```bash
dotnet user-secrets set "AdminCredentials:PasswordHash" "PBKDF2$210000$…$…"
```

…or an environment variable in production (note the double underscore):

```bash
AdminCredentials__PasswordHash=PBKDF2$210000$…$…
```

Serve over HTTPS wherever the network is not fully trusted. The auth cookie is `HttpOnly`,
`SameSite=Lax` and marked `Secure` on HTTPS requests (`CookieSecurePolicy.SameAsRequest`),
which keeps a plain-HTTP LAN setup in the van working.

---

## 8. Business rules implemented

**Products / categories**

- Name and price are required; price must be greater than 0.
- Product and category names must be unique; names are trimmed.
- Only active products in active categories can be sold.
- Nothing that has history is ever physically deleted — `IsActive` is used instead.

**Sales**

- A sale must contain at least one item; quantity must be 1–1000.
- Unit prices always come from the database at the time the sale is created.
- Totals are calculated server-side; browser-supplied totals are ignored.
- `Sale` and `SaleItems` are saved in one transaction.
- `SaleDate` is set automatically in UTC.

**Reports**

- `Date From` may not be later than `Date To`.
- A non-existent sale ID or product/category id produces a friendly message, not an error.
- Default view is today; clear both dates and press *Apply Filters* for all time.

---

## 9. Assumptions

1. **Single currency**, formatted as US dollars. Formatting is pinned to `en-US` in
   `Program.cs` so output does not change with the machine's regional settings.
2. **No stock levels.** Products are a price list, not an inventory with quantities on hand
   (complex inventory management was explicitly out of scope).
3. **No sale editing, voiding or refunds.** Sales are append-only; corrections would need a
   new feature.
4. **Local time = server time.** There is no per-user timezone; the van's laptop clock is the
   reference.
5. **Category is a current attribute, not a historical snapshot** (see
   [Database structure](#3-database-structure)).
6. **One admin, one van.** No roles, no multi-tenancy, no online payments.
7. **Quantity cap of 1000 per product line**, as a sanity guard against typos and tampering.
8. **Reports cap at 500 rows** per query, with a visible warning when the cap is reached.
9. Prices are limited to two decimal places (integer-cent storage).

---

## 10. Verified behaviour

Checked end-to-end against a running instance:

- Project builds with 0 warnings and 0 errors; database is created automatically.
- Login works; a wrong password is rejected with a generic message; logout works and
  protected pages then redirect to `/Account/Login?ReturnUrl=…`.
- Products and categories can be added and edited; duplicate names and missing/invalid
  values are rejected server-side (client-side validation bypassed during testing).
- Active products appear on the Sales page, grouped by category; category pills and search
  filter correctly, including in combination.
- Deactivating a product — or its category — removes it from the Sales page, and submitting
  it anyway is rejected.
- A sale can be completed; cart quantities (+ / −, typed value, remove) all compute
  correctly; the total is recalculated server-side.
- Tampered payloads are handled: empty cart, quantity 5000, duplicate lines for one product,
  and injected `UnitPrice`/`Total` fields (ignored — the stored total came from the database).
- The invoice is generated from the saved sale, and only the receipt survives printing
  (verified by applying the print rules and measuring: navigation, footer, alerts and all
  buttons hidden; receipt ≈76 mm wide).
- Reports totals, item counts and the date/category/product/sale-ID filters are correct; an
  invalid date range and a missing sale ID produce friendly messages.
- Historical prices are immutable: after renaming `Chocolate Bar` → `Chocolate Bar Deluxe`
  and changing its price from `$1.50` to `$4.44`, sale #1 still shows `Chocolate Bar` at
  `$1.50` and the report totals are unchanged.
- The `AddCategoriesAndInvoiceSupport` migration was applied to a database that already held
  products and sales: all products were backfilled to `Uncategorized` with no data loss.
- A POST without an antiforgery token returns HTTP 400; unknown routes show a friendly 404.
