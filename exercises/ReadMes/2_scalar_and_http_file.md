# Exercise 2 — Scalar UI and the `.http` file

**Reference lesson:** [`../lessons/2_lesson_scalar_and_http_file.md`](../lessons/2_lesson_scalar_and_http_file.md)

By the end: Scalar serving your API docs at `/scalar/v1`, and a `BankingApi.http` file you can run every Account request from inside your editor.

---

## Core

### 2.1 Install and wire up Scalar

From inside `BankingApi/`:

```bash
dotnet add package Scalar.AspNetCore
```

Add the `using Scalar.AspNetCore;` directive and the `app.MapScalarApiReference();` call in `Program.cs`, wrapped in the existing `IsDevelopment()` block (exactly like lesson 2 step 2).

### 2.2 Write `BankingApi.http`

Replace the contents of the auto-generated `BankingApi.http` with request blocks for all five `Account` actions. Use a `@BankingApi_HostAddress` variable for the host.

Required blocks: `GET /api/Accounts`, `GET /api/Accounts/{id}`, `POST /api/Accounts`, `PUT /api/Accounts/{id}`, `DELETE /api/Accounts/{id}`. Each separated by `###`.

### Acceptance check

Run the app, then:

1. Browse to `http://localhost:<port>/scalar/v1` — you should see `AccountsController` in the left sidebar with all five operations listed. Click `GET /api/Accounts` → **Test Request** → **Send** and confirm you get a `200 OK` response.
2. Open `BankingApi.http` in your editor (VS Code with REST Client, VS 2022+, or Rider). The **Send Request** button appears above each `###` block. Click it on the `POST` block (with a sample body) and confirm you get a `201 Created` response.

---

## Stretch

### 2.S1 — Cover Branch and Customer in the `.http` file

(Requires exercises 1.S1 and/or 1.S2.) Extend `BankingApi.http` with request blocks for every action on `BranchesController` and `CustomersController`. Same shape as the Account blocks; same `@BankingApi_HostAddress` variable.

#### Acceptance

Clicking **Send Request** on each new block returns the expected status code (`200`, `201`, or `204`) without touching the terminal.

### 2.S2 — Add a second host variable

Add `@BankingApi_ProdHost = https://api.example.com` to the top of your `.http` file. Write one duplicated `GET /api/Accounts` block that uses `{{BankingApi_ProdHost}}` instead of `{{BankingApi_HostAddress}}`.

This won't return real data (there's no production server) — the point is to practise switching hosts without touching every line.

#### Acceptance

The duplicated block's URL, when hovered in your editor, resolves to `https://api.example.com/api/Accounts`. No other changes needed.

---

## Done?

You can explore your API from the browser (Scalar), run any request from your editor (`.http`), or curl it from the terminal. Pick whichever you prefer for the remaining exercises.

Commit, then move on to [Exercise 3](3_seeding.md).
