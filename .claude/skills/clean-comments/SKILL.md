---
name: clean-comments
description: 스크립트에서 불필요한 주석을 제거하고 중요한 주석만 남깁니다
argument-hint: [file-path or directory]
allowed-tools: Read, Write, Edit, Glob, AskUserQuestion
---

# Clean Comments Skill

스크립트 파일에서 **불필요한 주석을 제거**하고 **중요한 주석만 남기는** 스킬입니다.

## 실행 흐름

### 1. 대상 파일 확인

`$ARGUMENTS`가 비어있으면 사용자에게 질문합니다:

```
어떤 스크립트 파일/폴더를 정리할까요? (예: Assets/Scripts/Core/StationController.cs)
```

- 단일 파일 경로: 해당 파일만 처리
- 폴더 경로: 해당 폴더 내 모든 `.cs`, `.js`, `.ts`, `.py` 파일 처리

### 2. 주석 분류 기준

#### ❌ 제거 대상 (불필요한 주석)

| 유형 | 예시 |
|------|------|
| **자명한 코드 설명** | `int count = 0; // count를 0으로 초기화` |
| **코드 반복 설명** | `// 리스트에 아이템 추가` 바로 위에 `list.Add(item);` |
| **IDE 자동 생성 기본 주석** | `// Start is called before the first frame update` |
| **주석 처리된 코드** | `// oldFunction();` 또는 `/* obsoleteCode(); */` |
| **빈 TODO/FIXME** | `// TODO:` (내용 없음) |
| **불필요한 구분선** | `//-------------------` (의미 없는 장식) |
| **날짜/버전만 있는 주석** | `// 2023-01-01 modified` (히스토리는 Git에서 관리) |
| **이름/서명 주석** | `// Written by John` |
| **파일 끝 주석** | `// End of file` |
| **중괄호 설명** | `} // end if`, `} // end for` |

#### ✅ 유지 대상 (중요한 주석)

| 유형 | 예시 |
|------|------|
| **WHY 설명** | `// Unity UI Toolkit 버그로 인해 한 프레임 지연 필요` |
| **비즈니스 로직** | `// 3초 대기: 비누가 충분히 발포되는 시간` |
| **경고/주의사항** | `// WARNING: 이 순서를 변경하면 레이스 컨디션 발생` |
| **Workaround/Hack** | `// HACK: Unity 2022의 Input System 버그 우회` |
| **외부 의존성** | `// API v2.0에서 deprecated 예정, v3.0 마이그레이션 필요` |
| **복잡한 알고리즘** | `// Dijkstra 알고리즘으로 최단 경로 계산` |
| **XML 문서 주석** | `/// <summary>`, `/// <param>`, `/// <returns>` |
| **라이선스/저작권** | `// MIT License`, `/* Copyright 2024 */` |
| **중요한 TODO** | `// TODO: 성능 최적화 - O(n²) → O(n log n)` |
| **매직 넘버 설명** | `// 0.5f = 물리 엔진 권장 fixed timestep` |
| **조건부 컴파일** | `#if UNITY_EDITOR`, `#region` (주석은 아니지만 유지) |

### 3. 처리 단계

1. **파일 읽기**: 대상 파일을 읽습니다
2. **주석 분석**: 각 주석을 분류 기준에 따라 평가합니다
3. **제거 계획 표시**: 제거할 주석 목록을 사용자에게 보여줍니다
4. **확인 요청**: 사용자 동의 후 진행합니다
5. **주석 제거**: Edit 도구로 불필요한 주석을 제거합니다
6. **결과 요약**: 제거된 주석 수와 유지된 주석 수를 보고합니다

### 4. 출력 형식

```
## 주석 정리 결과: [파일명]

### 제거된 주석 (N개)
- Line 15: `// count를 0으로 초기화` → 자명한 설명
- Line 42: `// oldMethod();` → 주석 처리된 코드
- Line 78: `// -----------` → 불필요한 구분선

### 유지된 주석 (M개)
- Line 5: `/// <summary>` → XML 문서
- Line 23: `// HACK: Unity 버그 우회` → Workaround 설명
- Line 56: `// 3초 = 비누 발포 시간` → 비즈니스 로직

### 요약
- 총 주석: X개
- 제거: N개 (Y%)
- 유지: M개 (Z%)
```

## 주의사항

### DO ✅
- 코드 로직은 절대 변경하지 않습니다
- 제거 전 항상 사용자에게 확인합니다
- 불확실한 경우 유지합니다 (보수적 접근)
- 빈 줄이 연속으로 남으면 하나로 정리합니다

### DON'T ❌
- 코드를 삭제하거나 수정하지 않습니다
- XML 문서 주석(`///`)을 제거하지 않습니다
- 저작권/라이선스 주석을 제거하지 않습니다
- TODO에 구체적인 내용이 있으면 제거하지 않습니다
- `#region`/`#endregion`을 제거하지 않습니다

## 예시

### Before
```csharp
// StationController.cs
// Created: 2024-01-15
// Author: Developer

using UnityEngine;

/// <summary>
/// 세척 스테이션을 제어합니다.
/// </summary>
public class StationController : MonoBehaviour
{
    // 비누 분사 시간
    private float _soapTime = 3f; // 3초로 설정

    // Start is called before the first frame update
    void Start()
    {
        // 초기화
        Initialize();
    }

    // HACK: Unity의 코루틴 타이밍 버그로 1프레임 대기 필요
    private void Initialize()
    {
        // _isReady = true;
        _isReady = true;
    }
} // end class
```

### After
```csharp
using UnityEngine;

/// <summary>
/// 세척 스테이션을 제어합니다.
/// </summary>
public class StationController : MonoBehaviour
{
    private float _soapTime = 3f;

    void Start()
    {
        Initialize();
    }

    // HACK: Unity의 코루틴 타이밍 버그로 1프레임 대기 필요
    private void Initialize()
    {
        _isReady = true;
    }
}
```

**제거된 주석:**
- 파일 헤더 (날짜, 작성자)
- `// 비누 분사 시간` (변수명으로 충분)
- `// 3초로 설정` (코드 반복)
- `// Start is called before the first frame update` (IDE 자동 생성)
- `// 초기화` (메서드명으로 충분)
- `// _isReady = true;` (주석 처리된 코드)
- `} // end class` (불필요한 구분)

**유지된 주석:**
- `/// <summary>` (XML 문서)
- `// HACK:` (Workaround 설명)
