---
name: explain-code
description: Generate line-by-line technical documentation and inline comments for a script
argument-hint: [file-path]
allowed-tools: Read, Write, Edit, Glob, AskUserQuestion
---

# Explain Code Skill

스크립트 파일의 코드를 한 줄씩 분석하여 **기술적인 설명 문서**를 생성합니다.

## 실행 흐름

### 1. 파일 경로 확인

`$ARGUMENTS`가 비어있으면 사용자에게 스크립트 경로를 질문합니다:

```
어떤 스크립트 파일을 분석할까요? (예: Assets/Scripts/Core/StationController.cs)
```

### 2. 파일 분석

파일을 읽고 다음을 수행합니다:
- 코드의 각 줄을 분석

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

## 지시사항 요약

1. `$ARGUMENTS`가 없으면 AskUserQuestion으로 파일 경로 요청
2. 파일을 Read로 읽기
3. 코드 분석하여 `_explained.md` 문서 Write로 생성

## 금지 사항

- 코드 로직 변경 금지
- 변수명/메서드명 변경 금지
- 포맷팅 변경 금지
- 포괄적/추상적 설명 금지 → **코드 동작 위주의 기술적 설명만**
