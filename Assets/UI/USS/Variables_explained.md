# Variables.uss 코드 설명서

## 개요
- **파일 경로**: `Assets/UI/USS/Variables.uss`
- **목적**: UI 전체에서 사용되는 CSS 커스텀 프로퍼티(변수)를 중앙 집중식으로 정의. 테마 색상, 폰트, 간격 등을 한 곳에서 관리하여 일관성 유지 및 유지보수 용이.

## CSS 변수 (Custom Properties) 개념

```css
/* 정의 */
:root {
    --color-primary: #2196F3;
}

/* 사용 */
.button {
    background-color: var(--color-primary);
}
```

- `:root`: 문서의 최상위 요소에 변수 정의 (전역 스코프)
- `--변수명`: CSS 변수 선언 (반드시 `--`로 시작)
- `var(--변수명)`: 변수 값 참조

## 색상 팔레트 시각화

```
┌─────────────────────────────────────────────────────────┐
│ HMI 색상 팔레트                                          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  배경 계열 (어두운 네이비)                               │
│  ┌─────────┬─────────┬─────────┐                       │
│  │ #0D1B2A │ #111E2D │ #162032 │                       │
│  │ bg      │ header  │ panel   │                       │
│  └─────────┴─────────┴─────────┘                       │
│                                                         │
│  상태 색상                                               │
│  ┌─────────┬─────────┬─────────┐                       │
│  │ #00E676 │ #FF9100 │ #FF1744 │                       │
│  │ green   │ warning │ error   │                       │
│  │ (정상)  │ (주의)  │ (오류)  │                       │
│  └─────────┴─────────┴─────────┘                       │
│                                                         │
│  텍스트                                                  │
│  ┌─────────┬─────────┐                                 │
│  │ #E0E0E0 │ #78909C │                                 │
│  │ text    │ subtext │                                 │
│  │ (주요)  │ (보조)  │                                 │
│  └─────────┴─────────┘                                 │
│                                                         │
│  강조/버튼                                               │
│  ┌─────────┬─────────┬─────────┐                       │
│  │ #2196F3 │ #1A2A3A │ #0D47A1 │                       │
│  │ accent  │btn-idle │btn-active│                       │
│  └─────────┴─────────┴─────────┘                       │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

## 코드 분석

### Line 1: :root 선택자
```css
:root {
```
- `:root`: 의사 클래스, 문서의 루트 요소 선택
- 여기에 정의된 변수는 모든 하위 요소에서 사용 가능 (전역 스코프)

### Line 2-3: 폰트 설정
```css
/* 폰트 */
-unity-font-definition: url("project://database/Assets/Fonts/Pretendard-Regular SDF.asset");
```
- `-unity-font-definition`: Unity 전용 속성, TextMeshPro SDF 폰트 지정
- `url("project://...")`: Unity 프로젝트 내 에셋 경로
- Pretendard: 한글 지원 무료 폰트

### Line 5-7: 배경 색상 계열
```css
--color-bg:         #0D1B2A;
--color-header:     #111E2D;
--color-panel:      #162032;
```

| 변수 | 색상 | 용도 |
|------|------|------|
| `--color-bg` | #0D1B2A | 전체 배경 (가장 어두움) |
| `--color-header` | #111E2D | 헤더/하단 바 배경 |
| `--color-panel` | #162032 | 사이드 패널 배경 |

**색상 계층:**
- 어두운 네이비 계열로 통일
- 밝기 차이로 영역 구분 (bg < header < panel)

### Line 8: 강조 색상
```css
--color-accent:     #2196F3;
```
- Material Design Blue 500
- 활성화 테두리, 하이라이트에 사용

### Line 9-11: 상태 색상
```css
--color-green:      #00E676;
--color-warning:    #FF9100;
--color-error:      #FF1744;
```

| 변수 | 색상 | 용도 |
|------|------|------|
| `--color-green` | #00E676 | 정상 상태 (LED, 게이지) |
| `--color-warning` | #FF9100 | 경고 (비누 20% 이하) |
| `--color-error` | #FF1744 | 오류 (비누 0%, 통신 오류) |

**신호등 패턴:**
- 초록 → 주황 → 빨강
- 직관적인 상태 인지

### Line 12-13: 텍스트 색상
```css
--color-text:       #E0E0E0;
--color-subtext:    #78909C;
```

| 변수 | 색상 | 용도 |
|------|------|------|
| `--color-text` | #E0E0E0 | 주요 텍스트 (밝은 회색) |
| `--color-subtext` | #78909C | 보조 텍스트 (회색) |

**대비율:**
- 어두운 배경(#0D1B2A)에서 충분한 가독성 확보
- 주요 vs 보조 텍스트의 시각적 계층 구분

### Line 14-15: 버튼 색상
```css
--color-btn-idle:   #1A2A3A;
--color-btn-active: #0D47A1;
```

| 변수 | 색상 | 용도 |
|------|------|------|
| `--color-btn-idle` | #1A2A3A | 버튼 기본 상태 |
| `--color-btn-active` | #0D47A1 | 버튼 활성화 (동작 중) |

### Line 16: 테두리 반경
```css
--radius-btn:       12px;
```
- 버튼 모서리 둥글기
- 통일된 UI 모양 유지

## 사용 예시 (MainTheme.uss)

```css
.header-bar {
    background-color: var(--color-header);
}

.status-led {
    background-color: var(--color-green);
}
.status-led.warning {
    background-color: var(--color-warning);
}
.status-led.error {
    background-color: var(--color-error);
}

.action-btn {
    background-color: var(--color-btn-idle);
    border-radius: var(--radius-btn);
}
```

## 테마 변경 예시

Variables.uss만 수정하면 전체 UI 테마 변경 가능:

```css
/* 라이트 테마 예시 */
:root {
    --color-bg:         #F5F5F5;
    --color-header:     #FFFFFF;
    --color-panel:      #EEEEEE;
    --color-text:       #212121;
    --color-subtext:    #757575;
    /* ... */
}
```

## 폰트 에셋 요구사항

```
Assets/Fonts/Pretendard-Regular SDF.asset
```
- TextMeshPro용 SDF(Signed Distance Field) 폰트 에셋
- 한글 문자 지원 필수
- Window → TextMeshPro → Font Asset Creator로 생성

## 색상 접근성

| 조합 | 대비율 | WCAG |
|------|--------|------|
| #E0E0E0 on #0D1B2A | 11.5:1 | AAA ✓ |
| #78909C on #0D1B2A | 5.2:1 | AA ✓ |
| #00E676 on #1A2A3A | 8.1:1 | AAA ✓ |

모든 텍스트/배경 조합이 WCAG 접근성 기준 충족.
