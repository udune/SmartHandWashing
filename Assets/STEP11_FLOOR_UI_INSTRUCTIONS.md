# STEP 11 — 층수 선택 UI (Claude Code 작업 지시서)

> **전제 조건:** STEP 4~10 구현 완료 상태  
> **C# 스크립트 수정 없음** — UXML + USS 만으로 완성  
> **선택 기능 없음** — 표시 전용, 8F 고정 선택 상태  
> **수정 파일:** `MainHMI.uxml`, `MainTheme.uss` 2개만

---

## 수정 파일 목록

```
-- 기존 파일 수정만 --
Assets/UI/UXML/MainHMI.uxml     ← floor-panel 블록 추가
Assets/UI/USS/MainTheme.uss     ← floor 관련 스타일 추가
```

---

## 11-1. MainTheme.uss 수정 — 층수 패널 스타일 추가

기존 `MainTheme.uss` 파일 **맨 아래에** 아래 스타일을 추가해줘:

```css
/* ════════════════════════════════════════
   층수 선택 패널 (표시 전용)
   ════════════════════════════════════════ */

.floor-panel {
    width: 168px;
    background-color: var(--color-panel);
    border-right-width: 1px;
    border-right-color: #1E3A5F;
    padding: 10px 8px;
    overflow: hidden;
}

.floor-panel-title {
    color: var(--color-subtext);
    font-size: 12px;
    margin-bottom: 8px;
    -unity-text-align: middle-center;
}

/* ── 층 항목 공통 ── */
.floor-item {
    min-height: 48px;
    background-color: var(--color-btn-idle);
    border-radius: 8px;
    border-width: 1px;
    border-color: #1E3A5F;
    margin-bottom: 5px;
    justify-content: center;
    align-items: center;
    padding: 4px 10px;
}

.floor-item-text {
    color: var(--color-subtext);
    font-size: 12px;
    -unity-text-align: middle-center;
    white-space: normal;
}

/* ── 선택된 층 (8F 고정) ── */
.floor-item.selected {
    background-color: #1B5E20;
    border-color: #00E676;
    border-width: 1px;
}

.floor-item.selected .floor-item-text {
    color: #00E676;
    -unity-font-style: bold;
}
```

---

## 11-2. MainHMI.uxml 수정 — floor-panel 블록 삽입

기존 `MainHMI.uxml` 에서 아래 위치를 찾아줘:

```xml
<!-- 찾을 위치: left-panel 닫는 태그 바로 뒤, viewer-container 열기 전 -->
        </ui:VisualElement>   ← left-panel 닫는 태그

        <ui:VisualElement name="viewer-container" ...>   ← viewer-container
```

그 사이에 아래 블록을 삽입해줘:

```xml
            <!-- 층수 선택 패널 (표시 전용, 8F 고정 선택) -->
            <ui:VisualElement name="floor-panel" class="floor-panel">
                <ui:Label class="floor-panel-title" text="위치 선택"/>

                <ui:VisualElement class="floor-item">
                    <ui:Label class="floor-item-text" text="1F - 꿈드림공작소"/>
                </ui:VisualElement>

                <ui:VisualElement class="floor-item">
                    <ui:Label class="floor-item-text" text="2F - 학장실"/>
                </ui:VisualElement>

                <ui:VisualElement class="floor-item">
                    <ui:Label class="floor-item-text" text="3F - 행정처 / 교무기획처"/>
                </ui:VisualElement>

                <ui:VisualElement class="floor-item">
                    <ui:Label class="floor-item-text" text="4F - 친환경산업디자인학과"/>
                </ui:VisualElement>

                <ui:VisualElement class="floor-item">
                    <ui:Label class="floor-item-text"
                              text="5F - 기계과 / 친환경산업디자인학과"/>
                </ui:VisualElement>

                <ui:VisualElement class="floor-item">
                    <ui:Label class="floor-item-text"
                              text="6F - 미래형자동차학과 / 메카트로닉스학과"/>
                </ui:VisualElement>

                <ui:VisualElement class="floor-item">
                    <ui:Label class="floor-item-text" text="7F - 메카트로닉스학과"/>
                </ui:VisualElement>

                <!-- 8F: 선택된 층 — selected 클래스 부착 -->
                <ui:VisualElement class="floor-item selected">
                    <ui:Label class="floor-item-text" text="8F - 스마트자동화학과"/>
                </ui:VisualElement>

            </ui:VisualElement>
            <!-- // 층수 선택 패널 -->
```

---

## 완성 후 레이아웃 구조

```
content-area  (flex-direction: row)
  ├─ left-panel        (width: 100px)   ← 기존 비누 게이지
  ├─ floor-panel       (width: 168px)   ← 신규 층수 목록
  └─ viewer-container  (flex-grow: 1)   ← 기존 3D 뷰어 (자동 축소)
```

> `viewer-container` 는 `flex-grow: 1` 이므로 `floor-panel` 이 추가되면  
> 자동으로 남은 공간을 차지합니다. 별도 width 수정 불필요.

---

## 동작 확인 체크리스트

| 항목 | 확인 내용 |
|------|-----------|
| 층수 패널 위치 | 비누 게이지 오른쪽, 3D 뷰어 왼쪽에 표시 |
| 1F ~ 7F | 어두운 배경 + 회색 텍스트 |
| 8F | 초록 배경 + 초록 테두리 + 초록 굵은 텍스트 |
| 3D 뷰어 | floor-panel 추가 후에도 중앙에 정상 표시 |
| 클릭 반응 | 없음 (표시 전용) |
| 텍스트 줄바꿈 | 긴 이름(5F, 6F)이 두 줄로 자연스럽게 표시 |

---

## 추후 선택 기능 추가 시 (참고)

나중에 층수 클릭으로 스테이션을 전환하는 기능이 필요하면  
`HMIUIController.cs` 에 아래만 추가하면 됩니다 — UXML/USS 수정 불필요:

```csharp
// 각 floor-item에 클릭 이벤트 등록 예시
var floorItems = root.Query<VisualElement>(className: "floor-item").ToList();
foreach (var item in floorItems)
{
    item.RegisterCallback<ClickEvent>(e =>
    {
        // 기존 selected 제거
        floorItems.ForEach(f => f.RemoveFromClassList("selected"));
        // 클릭한 항목 selected 부착
        item.AddToClassList("selected");
    });
}
```
