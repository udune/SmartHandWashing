# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**SmartHandWashing** is a Unity 6 (6000.4.0f1) 3D Digital Twin simulator for a hand-washing station with IoT monitoring capabilities. The project uses Universal Render Pipeline (URP) and UI Toolkit for the HMI interface.

**Primary Language**: Korean (UI text and documentation), English (code)

## Architecture

### Event-Driven MVC Pattern
- **Model**: `StationData` (ScriptableObject at `Assets/Resources/StationDataInstance.asset`)
- **View**: `HMIUIController` + UI Toolkit (UXML/USS)
- **Controller**: `StationController`

### Key Components
```
StationManager (GameObject)
└── StationController.cs - Manages operations (Soap 3s, Water 10s, Air 10s)
    └── Linked to StationData + ParticleSystems

3DViewer (GameObject)
├── ModelRoot - 3D model parent (HandWash.prefab)
└── ViewerCamera - RenderTexture camera (ViewerRT, 1024×1024)
    └── Culling Mask: "3DViewer" layer only

UIRoot (GameObject)
├── UIDocument - MainHMI.uxml
├── HMIUIController.cs - UI orchestration
└── ModelRotator.cs - Drag-to-rotate interaction

ParticleRoot (GameObject)
├── SoapParticle (inactive by default)
├── WaterParticle (inactive by default)
└── AirParticle (inactive by default)
```

### Script Locations
- `Assets/Scripts/Core/` - StationController.cs
- `Assets/Scripts/Data/` - StationData.cs (ScriptableObject)
- `Assets/Scripts/UI/` - HMIUIController.cs, ModelRotator.cs

### UI Assets
- `Assets/UI/UXML/MainHMI.uxml` - Main layout
- `Assets/UI/USS/Variables.uss` - CSS variables (colors, spacing)
- `Assets/UI/USS/MainTheme.uss` - Global styles

## Implementation Status

| Step | Description | Status |
|------|-------------|--------|
| STEP 4 | 폴더 구조 생성 | ✅ 완료 |
| STEP 5 | StationData.cs 생성 | ✅ 완료 |
| STEP 6 | UXML + USS 레이아웃 | ✅ 완료 |
| STEP 7 | C# 스크립트 작성 | ✅ 완료 |
| STEP 8 | 파티클 시스템 | ⏳ Unity 에디터 작업 필요 |

## Unity Editor Setup Required

다음 항목들은 Unity 에디터에서 직접 설정해야 합니다:

1. **"3DViewer" 레이어 생성**: Edit → Project Settings → Tags and Layers
2. **Input Handling 설정**: Project Settings → Player → Active Input Handling → "Both"
3. **StationDataInstance.asset 생성**: Assets/Resources/ 우클릭 → Create → SmartWash → Station Data
4. **MainPanelSettings.asset 생성**: Assets/UI/ 우클릭 → Create → UI Toolkit → Panel Settings Asset
5. **ViewerRT.renderTexture 생성**: Assets/RenderTextures/ 우클릭 → Create → Render Texture (1024×1024, ARGB32)
6. **씬 오브젝트 구성**: StationManager, 3DViewer, UIRoot, ParticleRoot GameObject 생성 및 컴포넌트 연결
7. **파티클 시스템 생성**: STEP 8 참조 (SoapParticle, WaterParticle, AirParticle)

## Code Conventions

- Private member variables: `_camelCase` prefix
- Event naming: `OnActionName` pattern
- Coroutine-based timing for dispenser operations
- UI element caching in `Start()` for performance
- Cleanup event subscriptions in `OnDestroy()`

## Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| com.unity.render-pipelines.universal | 17.4.0 | URP rendering |
| com.unity.inputsystem | 1.19.0 | Input handling |
| com.unity.visualeffectgraph | 17.4.0 | Particle effects |
| com.unity.test-framework | 1.6.0 | Unit testing |

## Common Issues

| Issue | Solution |
|-------|----------|
| Pink/magenta model | Change shader to `Universal Render Pipeline/Lit` |
| UI not visible | Connect Panel Settings to UIDocument |
| 3D viewer black | Check ViewerCamera Target Texture + Culling Mask |
| Drag not working | Set Active Input Handling to "Both" in Player Settings |
| Korean text broken (build) | Include Korean font (e.g., 나눔고딕) in Player Settings |

## Render Pipeline

Separate configurations for PC and Mobile:
- `Assets/Settings/PC_RPAsset.asset` - High-end
- `Assets/Settings/Mobile_RPAsset.asset` - Optimized

## Korean UI Text Reference

| Korean | English |
|--------|---------|
| 비누 | Soap |
| 물 | Water |
| 에어 드라이 | Air Dry |
| 비누 잔량 | Soap Level |
| 시스템 상태 | System Status |
| 정상/주의/오류 | Normal/Warning/Error |
