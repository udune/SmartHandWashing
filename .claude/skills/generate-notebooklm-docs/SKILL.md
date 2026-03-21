---
name: generate-notebooklm-docs
description: Generate 1-10 chapter learning documents for NotebookLM (podcast, video, infographic, PPT)
argument-hint: [output-folder-name]
allowed-tools: Read, Write, Glob, Grep
---

# NotebookLM 학습 문서 생성기

프로젝트의 코드를 분석하여 **NotebookLM**에서 활용 가능한 1~10 챕터의 상세 학습 문서를 생성합니다.

## 출력 위치

`$ARGUMENTS` 폴더에 챕터별 마크다운 파일을 생성합니다.
- 인자가 없으면 `NotebookLM_Docs` 폴더에 생성

## 생성되는 문서 구조 (10 챕터)

```
{output-folder}/
├── Chapter01_프로젝트_개요.md         # 프로젝트 소개, 목적
├── Chapter02_기술_스택.md             # Unity, URP, 패키지
├── Chapter03_아키텍처_패턴.md         # MVC, 이벤트 기반 설계
├── Chapter04_데이터_모델.md           # StationData, ScriptableObject
├── Chapter05_이벤트_시스템.md         # C# 이벤트, 구독/발행 패턴
├── Chapter06_비즈니스_로직.md         # StationController
├── Chapter07_코루틴_타이밍.md         # Coroutine, 시간 제어
├── Chapter08_UI_시스템.md             # UI Toolkit, HMIUIController
├── Chapter09_PLC_통신_기초.md         # SLMP 프로토콜, 디바이스
├── Chapter10_네트워크_통합.md         # NetworkManager, Mock 패턴
└── INDEX.md                           # 전체 목차 및 요약
```

## 각 챕터 작성 규칙

### 1. 대화체 설명
NotebookLM의 팟캐스트/동영상 생성을 위해 **대화체**로 작성합니다.

```markdown
## 이 코드는 무엇을 하나요?

StationController는 손 세정 스테이션의 "두뇌" 역할을 합니다.
사용자가 비누 버튼을 누르면, 이 컨트롤러가 "좋아, 비누를 3초간 분사하자!"라고
결정하고 실행하는 거죠.
```

### 2. 비유와 메타포 활용
```markdown
## 인터락(Interlock) 시스템

교통 신호등을 생각해보세요. 빨간불과 초록불이 동시에 켜지면 안 되죠?
마찬가지로, 비누/물/에어가 동시에 작동하면 안 됩니다.
IsAnyRunning() 함수가 이 "교통 경찰" 역할을 합니다.
```

### 3. 코드 블록 + 한글 주석
```markdown
```csharp
// 버튼을 누르면 실행되는 함수
public void ActivateSoap()
{
    // 다른 동작 중이면? → 무시!
    if (IsAnyRunning()) return;

    // 비누가 없으면? → 역시 무시!
    if (stationData.soapLevel <= 0f) return;

    // 모든 조건 통과 → 비누 분사 시작!
    StartCoroutine(RunDispenser(...));
}
```
```

### 4. Q&A 섹션 포함
```markdown
## 자주 묻는 질문

**Q: 왜 Coroutine을 사용하나요?**
A: Unity에서 "3초 동안 기다려"를 구현하는 가장 자연스러운 방법이에요.
   일반 함수로는 "기다리는 동안 다른 일 하기"가 어렵거든요.

**Q: ScriptableObject는 왜 쓰나요?**
A: 게임이 실행 중이 아닐 때도 데이터를 저장할 수 있어요.
   Inspector에서 값을 조절하며 테스트하기에도 편하고요.
```

### 5. 시각적 다이어그램 (텍스트 기반)
```markdown
## 데이터 흐름

┌─────────────┐     이벤트      ┌─────────────┐
│ StationData │ ──────────────▶ │    UI       │
│   (Model)   │                 │   (View)    │
└─────────────┘                 └─────────────┘
       ▲                               │
       │          사용자 입력           │
       │ ◀─────────────────────────────┘
       │
┌─────────────┐
│ Controller  │
│  (로직)     │
└─────────────┘
```

### 6. 핵심 개념 하이라이트
```markdown
> 💡 **핵심 포인트**: Event-Driven 패턴은 "변화가 생기면 알려줘" 방식입니다.
> UI가 매 프레임 "데이터 바뀌었어?"라고 묻는 대신,
> 데이터가 "나 바뀌었어!"라고 알려주는 거죠.
```

## 지시사항

1. **코드 분석**: 프로젝트의 모든 스크립트 파일을 읽습니다
2. **구조 파악**: 아키텍처, 의존성, 데이터 흐름을 분석합니다
3. **챕터 구성**: 논리적 순서로 10개 챕터를 구성합니다
4. **문서 작성**: 위 규칙에 따라 각 챕터를 작성합니다
5. **INDEX 생성**: 전체 목차와 학습 로드맵을 작성합니다

## 분량 가이드 (10 챕터)

| 챕터 | 권장 분량 | 설명 |
|------|----------|------|
| 01. 프로젝트 개요 | 600~800자 | 프로젝트 소개, 디지털 트윈 |
| 02. 기술 스택 | 600~800자 | Unity, URP, 패키지 |
| 03. 아키텍처 패턴 | 800~1000자 | MVC, 이벤트 기반 |
| 04. 데이터 모델 | 800~1000자 | StationData 상세 |
| 05. 이벤트 시스템 | 800~1000자 | C# 이벤트, 구독 패턴 |
| 06. 비즈니스 로직 | 1000~1200자 | StationController |
| 07. 코루틴 타이밍 | 800~1000자 | Coroutine 심화 |
| 08. UI 시스템 | 1000~1200자 | UI Toolkit, HMI |
| 09. PLC 통신 기초 | 1000~1200자 | SLMP, 프로토콜 |
| 10. 네트워크 통합 | 1000~1200자 | NetworkManager |

## NotebookLM 최적화 팁

- **짧은 문단**: 3~4문장 단위로 끊기
- **명확한 제목**: 계층 구조 명확히 (##, ###)
- **키워드 강조**: **굵게** 처리
- **실제 사례**: 구체적 시나리오 포함
- **한글 우선**: 기술 용어는 영어, 설명은 한글

## 챕터별 학습 목표

| 챕터 | 배우는 것 |
|------|----------|
| 01~03 | 큰 그림 이해 (Why & What) |
| 04~05 | 데이터 흐름 이해 |
| 06~07 | 핵심 로직 이해 (How) |
| 08 | UI 구현 방법 |
| 09~10 | 외부 시스템 연동 |
