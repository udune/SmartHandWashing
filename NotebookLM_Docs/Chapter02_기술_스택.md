# Chapter 02: 기술 스택 - Unity와 핵심 패키지

## 사용된 기술 한눈에 보기

```
┌──────────────────────────────────────────────────────────┐
│                    SmartHandWash 기술 스택                │
├──────────────────────────────────────────────────────────┤
│                                                          │
│   🎮 엔진          Unity 6 (6000.4.0f1)                  │
│   🎨 렌더링        URP (Universal Render Pipeline)       │
│   🖥️ UI           UI Toolkit (UXML/USS)                 │
│   🎮 입력          New Input System                      │
│   ✨ 이펙트        Visual Effect Graph                   │
│   📡 통신          SLMP Protocol (TCP/IP)                │
│   💻 언어          C# (.NET Standard 2.1)                │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## Unity 6란?

**Unity**는 전 세계에서 가장 많이 쓰이는 게임 엔진이에요.
근데 게임만 만드는 게 아니에요!

```
Unity로 만들 수 있는 것들:
├── 🎮 게임 (모바일, PC, 콘솔)
├── 🏭 산업용 시뮬레이터 ← 우리 프로젝트!
├── 🏠 건축 시각화
├── 🎓 교육용 콘텐츠
├── 🥽 VR/AR 애플리케이션
└── 🎬 영화 프리비즈
```

Unity 6는 2024년에 출시된 최신 버전으로,
성능과 그래픽이 크게 향상되었습니다.

---

## URP (Universal Render Pipeline)

> 💡 **핵심 개념**: 렌더 파이프라인은 "그림 그리는 방법"이에요.

```
┌──────────────────────────────────────────────────────────┐
│               Unity 렌더 파이프라인 종류                   │
├────────────────────┬─────────────────────────────────────┤
│  Built-in RP       │  옛날 방식, 유연성 낮음               │
├────────────────────┼─────────────────────────────────────┤
│  URP ✅            │  범용적, PC/모바일 모두 최적화         │
├────────────────────┼─────────────────────────────────────┤
│  HDRP              │  고사양, 영화급 그래픽                │
└────────────────────┴─────────────────────────────────────┘
```

이 프로젝트는 **URP**를 사용해요. PC와 모바일 모두에서 잘 작동하고,
산업용 애플리케이션에 적합한 균형 잡힌 성능을 제공합니다.

### PC vs Mobile 설정 분리

```
Assets/Settings/
├── PC_RPAsset.asset      ← 고사양 PC용 (그림자, 반사 등 풀옵션)
└── Mobile_RPAsset.asset  ← 모바일용 (최적화된 설정)
```

---

## UI Toolkit - 현대적인 UI 시스템

전통적인 Unity UI (UGUI)와 다른 새로운 방식이에요.
**웹 개발**과 비슷한 구조를 가지고 있죠.

```
웹 개발                    Unity UI Toolkit
─────────────────────────────────────────────
HTML (구조)         ←→     UXML (구조)
CSS (스타일)        ←→     USS (스타일)
JavaScript (동작)   ←→     C# (동작)
```

### 예시: 버튼 만들기

```xml
<!-- MainHMI.uxml - 구조 정의 -->
<ui:Button name="btn-soap" text="비누" class="control-btn"/>
```

```css
/* MainTheme.uss - 스타일 정의 */
.control-btn {
    background-color: #4CAF50;
    border-radius: 8px;
    padding: 12px;
}

.control-btn.active {
    background-color: #81C784;
}
```

```csharp
// C# - 동작 정의
_btnSoap.RegisterCallback<ClickEvent>(_ => {
    stationController.ActivateSoap();
});
```

---

## New Input System

Unity의 새로운 입력 처리 시스템이에요.
키보드, 마우스, 게임패드, 터치스크린 등 다양한 입력을 통합 관리합니다.

```
Project Settings → Player → Active Input Handling
├── Input Manager (Old) - 레거시 방식
├── Input System (New) - 새로운 방식
└── Both ✅ - 둘 다 사용 (우리 프로젝트!)
```

이 프로젝트에서는 **Both**로 설정해서
UI Toolkit의 마우스 이벤트와 호환성을 유지합니다.

---

## Visual Effect Graph

파티클 효과를 만드는 시각적 도구예요.
비누 거품, 물방울, 에어 바람 효과를 표현합니다.

```
┌──────────────────────────────────────────────────────────┐
│                   파티클 시스템                           │
├──────────────────────────────────────────────────────────┤
│                                                          │
│   🧴 SoapParticle   - 비누 거품 효과 (3초)                │
│   💧 WaterParticle  - 물방울 효과 (10초)                  │
│   💨 AirParticle    - 에어 바람 효과 (10초)               │
│                                                          │
│   기본 상태: 비활성화 (SetActive = false)                 │
│   분사 시: Play() 호출                                    │
│   종료 시: Stop() 호출                                    │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## 패키지 버전 정보

| 패키지 | 버전 | 용도 |
|--------|------|------|
| com.unity.render-pipelines.universal | 17.4.0 | URP 렌더링 |
| com.unity.inputsystem | 1.19.0 | 새 입력 시스템 |
| com.unity.visualeffectgraph | 17.4.0 | 파티클 효과 |
| com.unity.test-framework | 1.6.0 | 유닛 테스트 |

---

## 자주 묻는 질문

**Q: 왜 UGUI 대신 UI Toolkit을 쓰나요?**

A: UI Toolkit은 더 현대적이고, 웹 개발 경험이 있다면 친숙해요.
   스타일 시트(USS)로 디자인을 분리할 수 있어서 유지보수도 쉽고요.
   Unity의 미래 방향이기도 합니다.

**Q: URP는 필수인가요?**

A: 필수는 아니지만 권장해요. Built-in RP보다 성능이 좋고,
   최신 Unity 기능들이 URP에 먼저 추가되거든요.

---

## 다음 챕터 미리보기

다음 챕터에서는 이 프로젝트의 **아키텍처 패턴**을 살펴봅니다.
MVC 패턴과 이벤트 기반 설계가 어떻게 적용되었는지 알아볼 거예요!
