# Laundry & Dishes (LnD) - Unity LLM Test Generator 🧪🤖

**Laundry & Dishes (LnD)** is an automated tool for Unity designed to eliminate the repetitive work (the "dirty dishes") of creating unit tests. 

Leveraging the power of LLMs (Large Language Models) combined with the Roslyn compiler, LnD analyzes your project's scripts, understands the intention of your public methods, and automatically generates, validates, and compiles unit tests (via NUnit/Unity Test Framework) — all validated in-memory, without generating temporary files or breaking your project.

---

## 🚀 Operating Modes

LnD was built to adapt to your workflow, functioning both visually within the Editor and automatically via the Command Line Interface (CLI).

### 1. Editor Mode (GUI)
Ideal for day-to-day development, when you have just created or modified a class and need to ensure its test coverage.

**Step-by-step:**
1. In the Unity `Project` window, right-click the C# script you want to test.
2. In the context menu, select **`LnD > Generate Automated Tests`**.
3. A window (Hub) will open listing all the testable (public) methods of that class.
4. Select the methods you want to cover and click **Generate**.
5. LnD will communicate with the AI, generate the tests, compile them silently in the background, and update the Unity `Test Runner` automatically.

### 2. CLI Mode (Command Line)
Perfect for CI/CD pipelines or for running massive batch test generations overnight. 

To run via CLI, you must execute Unity in *batchmode*, calling LnD's static methods. 

**A) Generate tests for a specific file:**
```bash
<path_to_unity> -quit -batchmode -projectPath <path_to_project> -executeMethod LaundryNDishes.CLI.GenerateForFile -scriptPath "Assets/Scripts/MyScript.cs"

```

**B) Generate tests for an entire folder:**

```bash
<path_to_unity> -quit -batchmode -projectPath <path_to_project> -executeMethod LaundryNDishes.CLI.GenerateForFolder -folderPath "Assets/Scripts/Core"

```

*(Note: Replace the `-executeMethod` names above with the exact method names you defined in your CLI integration class).*

---

## ⚙️ Architecture and Performance

* **Zero Garbage:** The AI-generated tests are validated in RAM using `Microsoft.CodeAnalysis.CSharp` (Roslyn). No temporary `.cs` or `.dll` files are written to the disk during validation.
* **Smart Context:** Only the strictly necessary code is sent to the LLM, saving tokens and improving the AI's accuracy.
* **Integrated Database:** The status of generations and executions is saved in the `TestDatabase`, allowing you to manage and track which files need to be regenerated in future iterations.