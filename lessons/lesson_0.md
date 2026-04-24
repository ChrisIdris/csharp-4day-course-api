# Lesson 0 — The CLI and the IDE: two ways to do the same thing

Every step in the rest of this course is shown as a `dotnet` CLI command. That's on purpose: CLI commands are **copy-pasteable**, **cross-platform**, and **identical between your machine and a CI server**. They're also the easiest thing to put in a lesson because they leave no ambiguity.

But in your day-to-day work you'll almost certainly drive Visual Studio (or Rider, or VS Code) from **menus and dialogs**. Behind the scenes those menus call the same tools the CLI uses — a scaffolded controller is the same whether you right-clicked "Add Scaffolded Item…" or typed `dotnet aspnet-codegenerator controller`. Knowing both lets you:

- Follow the lessons step-for-step (CLI).
- Work the way most .NET developers work day-to-day (IDE).
- Reach for whichever is less friction for the job in front of you.

This lesson is a **reference**. Skim it once, then come back to it whenever a later lesson gives you a command and you'd rather do it from the IDE.

---

## Quick-reference table

Every action the course asks you to perform, with its CLI form and Visual Studio equivalent. Full details follow below.

| What you want to do | CLI | Visual Studio |
|---|---|---|
| Create a new Web API project | `dotnet new webapi --use-controllers -n Name -o Name` | **File → New → Project** → *ASP.NET Core Web API* |
| Open an existing project | (open folder in editor) | **File → Open → Project/Solution…** |
| Install a NuGet package | `dotnet add package <Id>` | Right-click project → **Manage NuGet Packages…** |
| Install a global .NET tool | `dotnet tool install -g <tool>` | **View → Terminal**, then run the CLI command |
| Update a global .NET tool | `dotnet tool update -g <tool>` | **View → Terminal**, then run the CLI command |
| Add a new folder | `mkdir Folder` | Right-click project → **Add → New Folder** |
| Add a new class / file | (create file in editor) | Right-click folder → **Add → Class…** |
| Delete a file | `rm <file>` | Select file → **Del** (or right-click → Delete) |
| Scaffold an EF CRUD controller | `dotnet aspnet-codegenerator controller …` | Right-click *Controllers* → **Add → New Scaffolded Item…** → *API Controller with actions, using Entity Framework* |
| Run the app | `dotnet run` | **F5** (with debugger) / **Ctrl+F5** (without) |
| Send a request from a `.http` file | `curl …` | Click **Send request** in the gutter |

---

## 1. Create a new Web API project

**Used in:** Lesson 1, step 1.

**CLI**

```bash
dotnet new webapi --use-controllers -n Lesson1 -o Lesson1
```

**Visual Studio**

1. **File → New → Project…**
2. In the template picker, type *ASP.NET Core Web API* and select it. Click **Next**.
3. **Project name:** `Lesson1`. **Location:** wherever your `lessons/` folder lives. Click **Next**.
4. On the **Additional information** page:
   - **Framework:** the .NET version you want (matches what `dotnet --version` reports).
   - **Configure for HTTPS:** ✅
   - **Enable OpenAPI support:** ✅
   - **Use controllers (uncheck to use minimal APIs):** ✅ — this is the equivalent of the CLI's `--use-controllers` flag. Leaving it unchecked would give you a Minimal-API `Program.cs`, which is **not** what this course uses.
   - **Do not use top-level statements:** ❌ (leave unchecked; the course assumes top-level `Program.cs`).
5. Click **Create**.

---

## 2. Install a NuGet package

**Used in:** Lesson 1, step 3 (EF Core + scaffolding packages), Lesson 2, step 1 (`Scalar.AspNetCore`).

**CLI**

```bash
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

**Visual Studio**

1. In **Solution Explorer**, right-click the project → **Manage NuGet Packages…**
2. Click the **Browse** tab.
3. Search for the package (e.g. `Microsoft.EntityFrameworkCore.InMemory`).
4. Select it, then click **Install** on the right-hand pane.
5. Repeat for each package the lesson asks you to install.

> **Shortcut — Package Manager Console:** **Tools → NuGet Package Manager → Package Manager Console**, then:
> ```powershell
> Install-Package Microsoft.EntityFrameworkCore.InMemory
> ```
> Same result, keyboard-friendly. This is the closest thing Visual Studio has to the `dotnet add package` command.

---

## 3. Install (or update) a global .NET tool

**Used in:** Lesson 1, step 3 — `dotnet-aspnet-codegenerator`.

Global tools are a `dotnet` concept, not a Visual Studio one — there is no GUI for them. You run the same CLI command either way; Visual Studio just gives you a convenient terminal to do it in.

**CLI (or Visual Studio terminal — same command)**

```bash
# one-time install
dotnet tool install -g dotnet-aspnet-codegenerator

# later, if scaffolding fails with an "old version" error
dotnet tool update -g dotnet-aspnet-codegenerator
```

**Visual Studio**

**View → Terminal** opens a Developer PowerShell pane inside the IDE. Run the command above in that pane.

---

## 4. Add a new folder

**Used in:** Lesson 1 (create `Models/`, `Data/`), Lesson 3 (reuse `Data/`).

**CLI**

```bash
mkdir Models
```

**Visual Studio**

In **Solution Explorer**, right-click the **project** (or a parent folder) → **Add → New Folder**. Type the folder name and press Enter.

---

## 5. Add a new class / file

**Used in:** Lesson 1 (`Models/TodoList.cs`, `Data/AppDbContext.cs`), Lesson 3 (`Data/DbSeeder.cs`).

**CLI**

Create the file in your editor of choice and paste the code from the lesson.

**Visual Studio**

1. Right-click the folder where the file belongs (e.g. `Models`) → **Add → Class…** (or **Add → New Item…** for more templates).
2. Name it (e.g. `TodoList.cs`) and click **Add**.
3. Visual Studio creates the file with the right namespace already filled in (based on the folder path) and opens it in the editor. Replace the stub with the lesson's code.

> Using **Add → Class…** is preferable to **Add → New Item…** for plain classes — it's one click shorter and the namespace defaulting is the same.

---

## 6. Delete a file

**Used in:** Lesson 1, step 2 (remove `WeatherForecast.cs` and `WeatherForecastController.cs`).

**CLI**

```bash
rm WeatherForecast.cs Controllers/WeatherForecastController.cs
```

**Visual Studio**

In **Solution Explorer**, select the file (Ctrl-click to select several), then press **Delete** (or right-click → **Delete**). Confirm the prompt.

---

## 7. Scaffold an EF CRUD controller

**Used in:** Lesson 1, step 6 — the biggest single step in the course.

**CLI**

```bash
dotnet aspnet-codegenerator controller \
  -name TodoListsController \
  -async \
  -api \
  -m Lesson1.Models.TodoList \
  -dc Lesson1.Data.AppDbContext \
  -outDir Controllers
```

**Visual Studio**

1. In **Solution Explorer**, right-click the **Controllers** folder → **Add → New Scaffolded Item…**
2. Choose **API Controller with actions, using Entity Framework**. Click **Add**.
3. In the dialog:
   - **Model class:** `TodoList (Lesson1.Models)` — equivalent to `-m Lesson1.Models.TodoList`.
   - **Data context class:** `AppDbContext (Lesson1.Data)` — equivalent to `-dc Lesson1.Data.AppDbContext`.
   - **Controller name:** `TodoListsController` — equivalent to `-name TodoListsController`.
   - Check **Use async controller actions** — equivalent to `-async`.
   - (The dialog defaults to *api* style because you picked the API scaffolder in step 2 — equivalent to `-api`.)
4. Click **Add**. Visual Studio runs `dotnet aspnet-codegenerator` under the hood and drops the same file into `Controllers/TodoListsController.cs`.

> The scaffolder **needs** the `Microsoft.EntityFrameworkCore.Design` and `Microsoft.VisualStudio.Web.CodeGeneration.Design` packages (plus the `SqlServer` quirk — see Lesson 1 step 3) whether you invoke it from the CLI or from the dialog. The dialog will fail with a vague error if any of those are missing.

---

## 8. Run the app

**Used in:** every lesson — step "Run it".

**CLI**

```bash
dotnet run
```

Kestrel starts, binds to the port from `Properties/launchSettings.json`, and prints the URL.

**Visual Studio**

- **F5** — run **with the debugger attached** (breakpoints work).
- **Ctrl+F5** — run **without** the debugger (starts faster, use this when you're just hitting endpoints).
- The **launch profile dropdown** next to the green *Play* button lets you pick between `http`, `https`, `IIS Express`, etc. These come from `Properties/launchSettings.json` — changing the profile here only affects the current session.

Either keybinding does the same thing as `dotnet run` plus attaches the IDE. Stop the app with **Shift+F5** or Ctrl+C in the terminal.

---

## 9. Send a request from a `.http` file

**Used in:** Lesson 2 onwards.

Visual Studio has had native support for `.http` files since **VS 2022 17.5** — no extension needed.

**CLI**

```bash
curl http://localhost:5107/api/TodoLists
```

**Visual Studio**

1. Make sure the API is running (`F5` / `Ctrl+F5` / `dotnet run`).
2. Open `Lesson2.http` (or whichever `.http` file the lesson provided).
3. A **Send request** button appears in the gutter above each `###`-separated request block. Click it.
4. The response (status line, headers, body) opens in a side pane.

> **VS Code:** install the *REST Client* extension; it adds the same "Send Request" CodeLens.
> **JetBrains Rider:** native support, same gutter button.

---

## "I'm on a Mac — where's Visual Studio?"

Microsoft **retired Visual Studio for Mac** in August 2024. On macOS, your options are:

- **JetBrains Rider** — the closest feature-parity replacement. Everything in this lesson's *Visual Studio* column has a Rider equivalent under the same broad menu shape (right-click project → Add / Manage NuGet Packages / etc.).
- **Visual Studio Code** with the **C# Dev Kit** extension — lighter-weight; the Dev Kit adds a Solution Explorer-like view, NuGet UI, debugging, and the same `.http` file support.
- **The `dotnet` CLI** in any terminal — the lessons' primary path. This always works and is what CI servers do anyway.

Mix and match freely. Nothing about a project created on the CLI is different from one created by Visual Studio — they're just folders of files described by a `.csproj`.

---

## Recap

- Every action the course shows as a CLI command has an equivalent Visual Studio menu path.
- When the two disagree, the **CLI is canonical** — that's what CI runs, and that's what the lessons are verified against.
- A few things (global `dotnet tool` install, anything scripted) are **only** CLI — even Visual Studio users fall back to the terminal for those.
- On macOS, replace *Visual Studio* with *Rider* or *VS Code + C# Dev Kit*; the conceptual steps are the same.

With this reference in mind, head to **Lesson 1** and pick whichever path feels more natural — just stay consistent within a single step.
