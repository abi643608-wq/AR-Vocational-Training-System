# AR Vocational Training System

A Unity prototype for augmented-reality fire-safety and evacuation training on Android. The existing project combines floor-based AR placement, guided extinguisher interaction, scenario questions, technique assessment, and a manually configured evacuation route. It is a training demonstrator, not certified instruction or an emergency navigation system.

## Implemented features

- AR floor detection and one-time fire placement, with grounded extinguisher placement beside the fire.
- Scenario questions for hazard identification and equipment choice, configured through a scenario asset.
- Extinguisher selection, animated safety-pin removal, reticle aiming, hold-to-spray controls, and simulated fire-health reduction when spraying at the target.
- Sweep assessment and feedback, integrated with the existing training controller.
- Manual placement and review of an exit, ordered waypoints, and an assembly marker on a detected floor.
- Ordered evacuation progress with proximity/dwell checks and guards for unreliable tracking or large camera jumps.
- Assessment recording, results presentation, local JSON attempt storage, and exercise restart/reconfiguration.
- Separate YOLO inference test scripts, an ONNX model, and a sample image. These experiments are distinct from the configured scenario questions; the app does not infer a real fire's material or a safe evacuation route from the camera.

These claims are based on the existing source and project notes. Historical notes describe earlier stages and may omit functionality added later; `SCENARIO_QUESTIONS.md` describes the prototype-2 question flow.

## Technologies

- Unity **6000.3.24f1**, C#, and Android.
- AR Foundation **6.3.5**, ARCore XR Plugin **6.3.5**, XR Management.
- Universal Render Pipeline **17.3.0** and Unity UI.
- Unity AI Inference **2.6.1** for the separate model experiments.

Dependency versions are recorded in `Packages/manifest.json` and `Packages/packages-lock.json`. The project retains its existing product name, `3D Mobile`, in Unity Player Settings.

## Setup

1. Install Unity 6000.3.24f1 through Unity Hub with Android Build Support, Android SDK/NDK tools, and OpenJDK.
2. Extract `UnityProject-Source.zip` from the GitHub repository into a new folder and obtain the dependencies described in `THIRD_PARTY_ASSETS.md` before building. The archive is the complete published source snapshot; the repository also exposes the main C# scripts for browsing.
3. Open the repository root as a Unity project and allow Unity to restore packages and import assets. Keep `Assets` and all matching `.meta` files together.
4. Open `Assets/Scenes/ARTraining.unity`. The existing scene is already wired; setup tools are documented in the stage notes and should only be used deliberately.
5. In **File > Build Profiles**, select the Android profile and confirm ARTraining is enabled. Preserve the project's XR, camera, URP renderer, and graphics settings.
6. Connect an ARCore-compatible Android device with USB debugging, select it as the run device, and build/run. Grant camera permission when requested.

## Usage

1. Scan a well-lit, textured horizontal floor with sufficient space to walk safely.
2. Configure and review the training exit, intermediate waypoints, and assembly marker. Confirm the route before starting.
3. Place the simulated fire and answer the configured hazard and equipment questions.
4. Select the extinguisher, release the placing gesture, remove its safety pin, then aim and hold the spray control. Follow sweep feedback.
5. After simulated extinction, identify the training exit and visit the route stops in order to reach assembly.
6. Review the assessment. Restart within the valid AR session or reconfigure the route for a new layout.

## Known limitations

- Training locations are selected by the demonstrator. There is no automatic exit detection, obstacle avoidance, validated emergency routing, or support for stairs/multiple floors.
- Floor alignment, tracking drift, touch interaction, and device performance need physical Android testing. Route coordinates are session-local and are not restored as persistent real-world anchors after relaunch.
- Scenario answers are tied to the supplied scenario. Camera imagery does not establish real fire type or extinguisher suitability.
- Scores and thresholds are prototype settings, not official safety standards. No certification is issued.
- Historical project notes report Editor/synthetic checks. No fresh Unity compilation, Android build, or phone test has been completed for this publication preparation; earlier results are not presented as a new test run.
- The restricted Vefects package, its copied fire prefab, and derived `SIH_Fire_*` materials are intentionally excluded. This source distribution is not ready to run with identical visuals: importing the vendor package alone does not restore the custom fire prefab or its scene references. See `THIRD_PARTY_ASSETS.md` for restoration requirements.

## Project layout

| Path | Purpose |
| --- | --- |
| `Assets/Scenes` | Existing Unity scenes |
| `Assets/Scripts/AR` | AR placement |
| `Assets/Scripts/Equipment` | Extinguisher, spray, fire, sweep, and input behavior |
| `Assets/Scripts/Training` | Scenarios, route, assessment, and local records |
| `Assets/Editor` | Existing setup/generation tools |
| `Assets/AI` | Inference experiments and model dependencies |
| `Packages` | Package manifest and lock file |
| `ProjectSettings` | Existing Unity configuration |
| `Tests` | Existing verification scripts; review their invocation before running |

Generated Library caches, logs, backups, editor state, and build outputs are excluded. The original working Unity project is preserved separately.

## License and attribution

Original project code is published under GNU AGPL-3.0; see `LICENSE`. The bundled Ultralytics YOLO11n ONNX model and sample image are attributed in `THIRD_PARTY_ASSETS.md`. Other retained third-party content remains subject to its own license notices; the project license does not override those terms.
