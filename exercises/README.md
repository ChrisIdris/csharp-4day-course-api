# BankingApi — exercises

This is the **practice** track for the ASP.NET Core Web API course. The `lessons/` folder teaches each concept against a Todos domain; here, you apply the same concepts to a domain you already know — the **Bank** from your console project.

## How this works

- You build **one project**, `BankingApi/`, that grows across every exercise.
- Each lesson has a matching `N_*.md` file here with its exercises.
- Before advancing to the next file, **make every core exercise pass**. Stretch exercises are optional — do them when you want extra reps on the lesson's idea.
- **No reference solutions are included.** The lesson markdown in `../lessons/N_*.md` walks through the same pattern against Todos; if you're stuck, read the lesson, then translate.

## Starting state

A fresh clone: you have `lessons/` and `exercises/` (this folder). `exercises/BankingApi/` does not exist yet — **Exercise 1 creates it**. From then on, every exercise modifies that one project.

## Grading contract

Every core exercise ends with an **acceptance check** — a short request and the response shape to expect. If the response matches, you're done with that exercise. If it doesn't, fix it before the next one — the next exercise builds on this one's state.

There is no partial credit: either the check passes or it doesn't. That's the discipline the course is after.

## Running the acceptance checks

Every acceptance check in these exercises is shown as a `curl` command — it's universal and works on every machine. You're free to run the same request any way you prefer:

- Paste the URL + headers + body into a `.http` file (introduced in Exercise 2) and click **Send**.
- Open **Scalar UI** at `/scalar/v1` (also Exercise 2) and use the "Test Request" panel.
- Use `curl` exactly as written.

The acceptance bar is the *response*, not the tool that produced it.

## The domain (recap)

Accounts and transactions, mirroring your Bank console project:

- An **`Account`** has an auto-generated account number (`ACC-1000`, `ACC-1001`, …) and a history of transactions. (Ownership — who the account belongs to — comes when we teach authentication.)
- A **`Transaction`** is a Credit or Debit with a positive decimal amount, a description, and a UTC timestamp.
- Transactions are **immutable** — once written, they never change.
- `Balance` is derived from transactions; we never store a balance field.

You've written every rule above in C# already. Here you're writing it behind HTTP.

Money is `decimal` everywhere. Timestamps are `DateTime.UtcNow`. If those rules aren't fresh, skim the "`decimal` mini-lesson" and "DateTime mini-lesson" in `../../csharp-4day-course/projects/bank/README.md`.

## The exercises

| # | File | Lesson |
|---|---|---|
| 1 | [`1_first_project.md`](1_first_project.md) | `1_lesson_new_api_project.md` |
| 2 | [`2_scalar_and_http_file.md`](2_scalar_and_http_file.md) | `2_lesson_scalar_and_http_file.md` |
| 3 | [`3_seeding.md`](3_seeding.md) | `3_lesson_seed_database.md` |
| 4 | [`4_updated_at.md`](4_updated_at.md) | `4_lesson_auto_stamp_updated_at.md` |
| 5 | [`5_transaction_and_relations.md`](5_transaction_and_relations.md) | `5_lesson_todo_entity_and_relations.md` |
| 6 | [`6_includes_and_cycles.md`](6_includes_and_cycles.md) | `6_lesson_related_records_and_cycles.md` |
| 7 | [`7_query_params.md`](7_query_params.md) | `7_lesson_query_params_and_filtering.md` |
| 8 | [`8_dtos.md`](8_dtos.md) | `8_lesson_dtos.md` |
| 9 | [`9_authentication.md`](9_authentication.md) | `authentication/auth_1_…`, `auth_2_…`, `auth_3_…` |

Start with Exercise 1.

> **Exercise 9 (authentication)** is bigger than the others — it covers the full three-lesson auth arc in one file. It's reachable from the end of Exercise 6 onwards (it doesn't require Exercises 7 or 8), so you can detour into auth early if that's the path you want.
