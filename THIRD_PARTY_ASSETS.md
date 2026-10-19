# Third-party assets and required imports

The source archive excludes restricted VFX and copied derivatives. The original working Unity project is unchanged. These omissions mean this public source snapshot needs asset restoration before it can reproduce the complete local application.

## Free Fire VFX — URP

- Existing path: `Assets/Vefects/Free Fire VFX URP/`.
- Publisher: Vefects.
- Asset Store: https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-fire-vfx-urp-266226
- Listed license: Standard Unity Asset Store EULA, https://unity.com/legal/as-terms
- Raw assets must not be included in a public source repository without appropriate permission. Modified or copied assets, including the project's fire prefab and derived effects/materials, require review as well.
- Excluded: the vendor package, `Assets/Prefabs/Fire/FireHazard.prefab` and its `.meta`, and `Assets/Materials/SIH_Equipment/SIH_Fire_*` materials and their metadata.
- Obtain/import the package through Unity's Asset Store workflow. The project-specific fire prefab and material changes are not automatically restored by importing it. You must obtain an appropriately licensed local copy or reconstruct this integration yourself. No replacement was invented for this publication.
- The retained scene has a missing fire-prefab reference until restoration. Reassign the restored prefab in the AR placement controller and verify its components, materials, and renderer configuration. Existing setup tools assume the fire prefab already exists and are not a one-click restoration procedure.

## Ultralytics YOLO

- Existing model: `Assets/AI/Models/yolo11n.onnx`.
- Existing sample: `Assets/AI/TestImages/bus.jpg`.
- Source attribution is recorded in `Assets/AI/TestImages/README.txt`.
- License information: https://www.ultralytics.com/license
- The project owner selected AGPL-3.0 publication. The project includes the full license text in `LICENSE` and retains attribution. The ONNX model is an Ultralytics YOLO11n model; the sample image comes from the Ultralytics repository. No ownership of those assets is claimed.
- Ultralytics source: https://github.com/ultralytics/ultralytics
- Sample image source: https://github.com/ultralytics/ultralytics/blob/main/ultralytics/assets/bus.jpg

## Other bundled assets

TextMesh Pro font files retain their SIL Open Font License notice in `Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt`. The EmojiOne sample sprite files are excluded; acquire them separately if required by your UI. Installed Unity packages are restored from the manifests rather than copied from Library/PackageCache. Vendor files are not relicensed as original project code.
