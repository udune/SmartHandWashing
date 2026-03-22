# MainTheme.uss 코드 설명서

## 개요
- **파일 경로**: `Assets/UI/USS/MainTheme.uss`
- **목적**: HMI UI의 전체 스타일을 정의하는 USS(Unity Style Sheet) 파일. CSS와 유사한 문법으로 UI Toolkit 요소의 레이아웃, 색상, 폰트 등을 지정.

## USS vs CSS 차이점

| 항목 | CSS | USS |
|------|-----|-----|
| 폰트 스타일 | `font-weight: bold` | `-unity-font-style: bold` |
| 배경 이미지 크기 | `background-size` | `-unity-background-scale-mode` |
| 변수 | `var(--name)` | `var(--name)` (동일) |
| 플렉스박스 | 동일 | 동일 |
| 선택자 | `.class`, `#id` | `.class`, `#name` (UXML name 속성) |

## 레이아웃 구조

```
┌────────────────────────────────────────────────────┐
│ .header-bar (50px)                                  │
│  ├─ .header-title    ├─ .header-datetime           │
│  └─ .status-container (.status-text + .status-led) │
├────────────────────────────────────────────────────┤
│ .content-area (flex-grow: 1)                        │
│  ┌───────────┬─────────────────────────────────┐   │
│  │.left-panel│         .viewer-container       │   │
│  │ (100px)   │         (flex-grow: 1)          │   │
│  │           │                                 │   │
│  │ 비누 게이지│         #3d-viewer              │   │
│  │           │         (RenderTexture)         │   │
│  └───────────┴─────────────────────────────────┘   │
├────────────────────────────────────────────────────┤
│ .bottom-bar (100px)                                 │
│  ┌─────────────┬─────────────┬─────────────┐       │
│  │ .action-btn │ .action-btn │ .action-btn │       │
│  │   (비누)    │    (물)     │   (에어)    │       │
│  └─────────────┴─────────────┴─────────────┘       │
└────────────────────────────────────────────────────┘
```

## 코드 분석

### Line 1-6: 루트 컨테이너
```css
.root-container {
    width: 100%;
    height: 100%;
    flex-direction: column;
    background-color: var(--color-bg);
}
```
- `width/height: 100%`: 부모 요소 전체 채움
- `flex-direction: column`: 세로 방향 플렉스 레이아웃 (헤더 → 콘텐츠 → 하단바)
- `var(--color-bg)`: Variables.uss에 정의된 CSS 변수 참조

### Line 9-18: 헤더 바
```css
.header-bar {
    height: 50px;
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 0 20px;
    background-color: var(--color-header);
    border-bottom-width: 1px;
    border-bottom-color: #1E3A5F;
}
```
- `flex-direction: row`: 가로 방향 배치
- `justify-content: space-between`: 양 끝 정렬 (타이틀 ↔ 날짜시간)
- `align-items: center`: 세로 중앙 정렬
- `padding: 0 20px`: 상하 0, 좌우 20px 패딩

### Line 19-27: 헤더 텍스트 스타일
```css
.header-title {
    color: var(--color-text);
    font-size: 16px;
    -unity-font-style: bold;
}
```
- `-unity-font-style: bold`: Unity 전용 속성, CSS의 `font-weight: bold`에 해당

### Line 28-44: 상태 LED
```css
.status-led {
    width: 14px;
    height: 14px;
    border-radius: 7px;
    background-color: var(--color-green);
}
.status-led.warning { background-color: var(--color-warning); }
.status-led.error   { background-color: var(--color-error);   }
```
- `border-radius: 7px`: 너비/높이의 절반 → 원형
- `.status-led.warning`: 복합 선택자, 두 클래스 모두 가진 요소
- C#에서 `AddToClassList("warning")`으로 동적 적용

### Line 47-50: 콘텐츠 영역
```css
.content-area {
    flex-grow: 1;
    flex-direction: row;
}
```
- `flex-grow: 1`: 남은 공간 전체 차지 (헤더/하단바 제외)
- `flex-direction: row`: 좌측 패널 + 3D 뷰어 가로 배치

### Line 53-60: 좌측 게이지 패널
```css
.left-panel {
    width: 100px;
    background-color: var(--color-panel);
    align-items: center;
    padding: 20px 0;
    border-right-width: 1px;
    border-right-color: #1E3A5F;
}
```
- `width: 100px`: 고정 너비
- `align-items: center`: 자식 요소 가로 중앙 정렬

### Line 71-83: 게이지 트랙/필
```css
.gauge-track {
    width: 28px;
    flex-grow: 1;
    background-color: #1A2A3A;
    border-radius: 4px;
    overflow: hidden;
    justify-content: flex-end;
}
.gauge-fill {
    width: 100%;
    background-color: var(--color-green);
    border-radius: 4px;
}
```
- `justify-content: flex-end`: 게이지 필이 아래에서 위로 채워짐
- `overflow: hidden`: 필이 트랙 밖으로 넘치지 않음
- C#에서 `style.height = Length.Percent(pct)`로 높이 동적 설정

### Line 99-113: 3D 뷰어
```css
.viewer-container {
    flex-grow: 1;
    align-items: center;
    justify-content: center;
    background-color: var(--color-bg);
}
#3d-viewer {
    -unity-background-scale-mode: scale-to-fit;
}
```
- `#3d-viewer`: UXML의 `name="3d-viewer"` 요소 선택 (ID 선택자처럼 동작)
- `-unity-background-scale-mode: scale-to-fit`: RenderTexture 비율 유지하며 맞춤

### Line 116-135: 하단 버튼 바
```css
.bottom-bar {
    height: 100px;
    flex-direction: row;
    background-color: var(--color-header);
    border-top-width: 1px;
    border-top-color: #1E3A5F;
    padding: 8px;
}
.action-btn {
    flex-grow: 1;
    flex-direction: row;
    align-items: center;
    justify-content: center;
    background-color: var(--color-btn-idle);
    border-radius: var(--radius-btn);
    margin: 0 6px;
    cursor: hand;
}
```
- `flex-grow: 1`: 세 버튼이 동일한 너비로 분배
- `cursor: hand`: 마우스 오버 시 손 모양 커서

### Line 136-142: 버튼 상태 스타일
```css
.action-btn:hover {
    background-color: #1E3A5F;
}
.action-btn.active {
    background-color: var(--color-btn-active);
    border-color: var(--color-accent);
}
```
- `:hover`: 마우스 오버 시 스타일 (의사 클래스)
- `.active`: 버튼 활성화 시 (C#에서 `AddToClassList("active")`)

### Line 160-167: 버튼 LED
```css
.btn-led {
    width: 10px;
    height: 10px;
    border-radius: 5px;
    background-color: #1A2A3A;
    margin-left: 12px;
}
.btn-led.active { background-color: var(--color-green); }
```
- 기본: 어두운 회색 (비활성)
- `.active`: 녹색 (동작 중)

## 사용된 CSS 변수 (Variables.uss에서 정의)

| 변수명 | 용도 |
|--------|------|
| `--color-bg` | 배경색 |
| `--color-header` | 헤더/하단바 배경 |
| `--color-panel` | 사이드 패널 배경 |
| `--color-text` | 주요 텍스트 색상 |
| `--color-subtext` | 보조 텍스트 색상 |
| `--color-green` | 정상 상태 (녹색) |
| `--color-warning` | 경고 상태 (노란색) |
| `--color-error` | 오류 상태 (빨간색) |
| `--color-btn-idle` | 버튼 기본 배경 |
| `--color-btn-active` | 버튼 활성화 배경 |
| `--color-accent` | 강조 색상 |
| `--radius-btn` | 버튼 모서리 반경 |

## Flexbox 레이아웃 요약

```
                    flex-direction
                    ┌──────────────────────┐
           column   │ 1 │ (주축: 세로)     │
                    │ 2 │                  │
                    │ 3 │                  │
                    └──────────────────────┘

                    ┌──────────────────────┐
              row   │  1  │  2  │  3  │     │ (주축: 가로)
                    └──────────────────────┘

justify-content: 주축 방향 정렬 (space-between, center, flex-end)
align-items: 교차축 방향 정렬 (center, stretch, flex-start)
flex-grow: 남은 공간 분배 비율
```

## C# 연동 예시

```csharp
// 클래스 추가/제거로 스타일 전환
_statusLed.AddToClassList("warning");
_statusLed.RemoveFromClassList("error");

// 인라인 스타일로 동적 값 설정
_gaugeFill.style.height = Length.Percent(75);
```
