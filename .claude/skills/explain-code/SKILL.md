---
name: explain-code
description: Generate line-by-line technical documentation and inline comments for a script
argument-hint: [file-path]
allowed-tools: Read, Write, Edit, Glob, AskUserQuestion
---

# Explain Code Skill

스크립트 파일의 코드를 한 줄씩 분석하여 **기술적인 설명 문서**를 생성하고, 원본 파일에 **인라인 주석**을 추가합니다.

## 실행 흐름

### 1. 파일 경로 확인

`$ARGUMENTS`가 비어있으면 사용자에게 스크립트 경로를 질문합니다:

```
어떤 스크립트 파일을 분석할까요? (예: Assets/Scripts/Core/StationController.cs)
```

### 2. 파일 분석

파일을 읽고 다음을 수행합니다:
- 코드의 각 줄을 분석
- 이미 주석이 있는 줄은 **스킵**
- 빈 줄, 중괄호만 있는 줄은 **스킵**

### 3. 출력물 생성

#### A. 설명 문서 (`.md` 파일)

원본 파일과 **같은 폴더**에 `[파일명]_explained.md` 생성

문서 형식:
```markdown
# [파일명] 코드 설명서

## 개요
- **파일 경로**: `경로`
- **목적**: 이 스크립트의 역할 요약

## 코드 분석

### Line 1-5: 네임스페이스 및 using 문
```csharp
using UnityEngine;
```
- `UnityEngine`: Unity 엔진의 핵심 클래스들 (MonoBehaviour, GameObject 등) 사용

### Line 7-15: 클래스 선언
```csharp
public class Example : MonoBehaviour
```
- `public`: 다른 스크립트에서 접근 가능
- `MonoBehaviour`: Unity 생명주기 메서드 (Start, Update 등) 사용 가능

... (각 코드 블록별로 계속)
```

#### B. 인라인 주석 추가

원본 스크립트 파일에 직접 주석 추가:

```csharp
// [SerializeField] 인스펙터에서 private 필드를 노출
[SerializeField] private float _speed;

// Awake(): 오브젝트 생성 시 가장 먼저 호출 (Start보다 먼저)
private void Awake()
{
    // GetComponent<T>(): 같은 GameObject의 컴포넌트 참조 획득
    _rigidbody = GetComponent<Rigidbody>();
}
```

## 주석 작성 규칙

### 주석을 추가하는 경우
- 변수/필드 선언
- 메서드 선언부
- Unity 생명주기 메서드 (Awake, Start, Update, OnDestroy 등)
- 복잡한 로직이나 API 호출
- LINQ 쿼리
- 코루틴
- 이벤트 구독/해제

### 주석을 스킵하는 경우
- 이미 주석이 달린 줄
- 빈 줄
- `{` 또는 `}` 만 있는 줄
- `#region` / `#endregion`
- 단순한 return 문 (예: `return;`, `return true;`)
- 너무 자명한 코드 (예: `i++`, `count = 0;`)

### 주석 스타일
- 한국어로 작성
- 간결하고 기술적인 설명 (인문학적 서술 X)
- Unity/C# API 사용 시 해당 API의 역할 명시
- 코드 바로 위 줄에 `//` 주석으로 작성

## 지시사항 요약

1. `$ARGUMENTS`가 없으면 AskUserQuestion으로 파일 경로 요청
2. 파일을 Read로 읽기
3. 코드 분석하여 `_explained.md` 문서 Write로 생성
4. 원본 파일에 Edit으로 인라인 주석 추가
5. 이미 주석이 충분한 부분은 건드리지 않음

## 금지 사항

- 코드 로직 변경 금지
- 변수명/메서드명 변경 금지
- 포맷팅 변경 금지
- 영어 주석 금지 (한국어만 사용)
- 포괄적/추상적 설명 금지 → **코드 동작 위주의 기술적 설명만**
