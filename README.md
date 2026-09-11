# NoesisGUI Integration Sample (C#)

[**NoesisGUI**](https://www.noesisengine.com/) is a lightweight, cross-platform XAML UI engine that renders
WPF/XAML interfaces (styles, data binding, animations, vector graphics) on top of a game engine's renderer.
This sample integrates NoesisGUI with UNIGINE from **C#**: it implements a custom Noesis render device on top
of the UNIGINE rendering API and shows XAML both as screen-space overlays and as a GUI mapped onto a 3D surface
in the world, wired to the engine through a data context (sun-angle and time-of-day controls).

This is the C# counterpart of the C++ NoesisGUI integration sample. The behaviour is identical; the
integration uses the managed **`Noesis.GUI`** binding instead of the C++ NoesisGUI SDK.

## How to Run the Sample

### Prerequisites

- [**UNIGINE SDK Browser**](https://developer.unigine.com/en/docs/latest/start/installing_sdk) (latest version)
- **UNIGINE SDK Community** or **Engineering** edition (**Sim** upgrade supported)
- **.NET 8 SDK**
- **Windows:** **Visual Studio 2022**
- **Linux:** the `dotnet` CLI is enough; [**VS Code**](https://code.visualstudio.com/download) or
  [**Rider**](https://www.jetbrains.com/rider/)

### Third-party dependency

This sample uses the managed **NoesisGUI C# binding**: NuGet packages **`Noesis.GUI`** and **`Noesis.App`**,
both version **3.2.13** ([nuget.org](https://www.nuget.org/packages/Noesis.GUI/)). `Noesis.GUI` is the runtime
binding; `Noesis.App` supplies the Interactivity package the sample uses (`NoesisApp.Interaction`).
Neither is bundled in this repository; both are restored automatically from nuget.org by the
`<PackageReference>` entries in the `.csproj` (the managed `Noesis.GUI.dll` is copied next to the
application in `bin/`).

> [!IMPORTANT]
> Use the **`Noesis.GUI`** package (the `Noesis` runtime namespace: `GUI` / `View` / `Renderer` /
> `RenderDevice` / providers). Do **not** use `Noesis.GUI.Extensions` — that is the Blend/WPF design-time
> helpers (`NoesisGUIExtensions` namespace) and does not contain the runtime binding.

**Native runtime library.** The same package carries the native NoesisGUI runtime for every platform under
`runtimes/<rid>/native/` (`win-x64/Noesis.dll`, `linux-x64/libNoesis.so`). The build copies them into
`bin/runtimes/<rid>/native/`, and the one for the platform you build on also next to the executable; the .NET
host resolves them through the application's `.deps.json`. Nothing has to be placed there by hand.


### Rendering backends

- **Windows:** Direct3D 12 or Vulkan. The Direct3D 12 backend loads the D3D12 Agility SDK redistributable from
  `bin/D3D12/` (relative to the executable).
- **Linux:** Vulkan.

### Fonts

The sample installs its own Noesis font provider, so the **only** fonts available are the ones shipped in
`data/ui/` — no system font is reachable. Every face is registered from its own folder, and a `FontFamily`
resolves only when it addresses the family through that folder: relative to the referencing XAML
(`Fonts/#PT Root UI` inside `data/ui/themes/noesis/`), or relative to the data root when the value has no
XAML context, i.e. comes from a binding or from the font fallback list (`ui/fonts/#Muli`).

`AppSystemLogic.cs` therefore points the fallback chain at a bundled face. Naming a system family such as
`Arial` there works on Windows only — Noesis resolves it through DirectWrite — and renders every character
as a `.notdef` box on Linux.

### Step-by-Step Guide

To get started with the **NoesisGUI C# Sample**:

1. **Clone or download** the sample.

2. **Open SDK Browser** and make sure you have the latest version.

3. **Add the sample project to SDK Browser**:
   - Go to the *My Projects* tab.
   - Click *Add Existing*, select the `.project` file from the cloned folder (matching your OS -
     `*_win_*`/`*_lin_*`, edition, precision), and click *Import Project*.

     ![Add Project](https://documentation-api.unigine.com/en/docs/latest/sdk/api_samples/third_party/photon/add_project.png)

4. **Repair the project**:
   - After importing, you'll see a **Repair** warning - this is expected, as only essential files are stored in
     the Git repository. SDK Browser will restore the rest.

   ![Repair Project](https://documentation-api.unigine.com/en/docs/latest/sdk/api_samples/third_party/repair_project.png)
   - Click *Repair* and then *Configure Project*.

5. **Open the project in Visual Studio**:
   - Launch **Visual Studio 2022** and open `unigine-noesis-csharp-integration-sample.sln`.
   - On **Linux** skip this step.

6. **Install the required NuGet packages**
   - In Visual Studio, go to **Tools → NuGet Package Manager → Manage NuGet Packages for Solution...**
   - Search for and install `Noesis.GUI` and `Noesis.App` (both version **3.2.13**).
   - Both are also restored automatically by the first build, so this step is only needed when that restore has
     not run. An internet connection is required the first time.
   - **Linux:** there is no Visual Studio - use the `dotnet` CLI, which restores the packages on build.

7. **Build** the project.
   - Pick the configuration that matches the precision of the `.project` you imported: **`Release`** /
     **`Debug`** for a `*_float.project`, **`Release-Double`** / **`Debug-Double`** for a `*_double.project`.
     The `-Double` configurations define `UNIGINE_DOUBLE` and link the `*_double_*` engine binding.
   - The binaries land in the project's `bin/`, next to the engine libraries.

8. **Launch** the project.
   - **Windows:** press *Run* in Visual Studio - the `main` profile in `Properties/launchSettings.json`
     passes the startup arguments and loads the `noesis_sample` world.
   - **Linux:** open a terminal in the folder the build wrote the binaries to - the project's `bin/`, next to
     the engine libraries - and start the application from there (the file name matches the configuration you
     built: `_x64` / `_x64d` / `_double_x64` / `_double_x64d`):
     ```
     LD_LIBRARY_PATH=. ./unigine-noesis-csharp-integration-sample_x64 -console_command "world_load noesis_sample"
     ```

## What the Sample Contains

```
unigine-noesis-csharp-integration-sample/
  source/     — main.cs (entry point), AppSystemLogic.cs / AppWorldLogic.cs, and the Noesis
                integration glue (NoesisIntegration, NoesisView, NoesisRenderDevice, NoesisTexture,
                NoesisProviders, NoesisShader, NoesisDataContext, NoesisGuiObject)
  data/       — noesis_sample.world, noesis/ (materials + shaders), ui/ (XAML, fonts, themes),
                root_mount.umount
  *.csproj / *.sln          — the C# / .NET project (net8.0, x64, four configurations)
  Properties/launchSettings.json
  README.md   — this file
  *.project   — SDK Browser project files (per platform / edition / precision)
```

The 3D panel is drawn with an `ObjectMeshDynamic` quad that samples a `RenderTarget` Noesis renders into
off-screen each frame (using `noesis/materials/noesis_gui_mesh.basemat`), since `ObjectExternBase` cannot be
subclassed from C#. The data context is idiomatic C# (`INotifyPropertyChanged` + string indexer + `ICommand`),
so the XAML `{Binding [key]}` expressions are unchanged from the C++ sample.

## If the Sample Fails to Run

- Re-check every setup step above.
- Ensure the **`Noesis.GUI`** and **`Noesis.App`** 3.2.13 NuGet packages restored (an internet connection is
  needed the first time), and that the build produced `bin/Noesis.GUI.dll` plus the native runtime in
  `bin/runtimes/<rid>/native/`. A native `Noesis.dll` / `libNoesis.so` left over from another NoesisGUI
  version directly in `bin/` takes precedence over the package's one and makes Noesis fail at init/load.
- On Windows with the Direct3D 12 backend, verify the D3D12 Agility SDK redistributable is present in `bin/D3D12/`.
- On Linux, run with `-video_app vulkan` (Direct3D 12 is Windows-only) and make sure `LD_LIBRARY_PATH` contains
  the project's `bin/`, so the engine and NoesisGUI native libraries are found.
- Ensure the build **configuration** matches the chosen `.project` precision (`*-Double` for double, plain for float).
- Use the `.project` file for your platform and SDK edition.
- Verify your SDK version is not older than the project's specified version.
