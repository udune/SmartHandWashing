---
name: plc-ladder-sync
description: >
  GX Works3에서 CSV로 내보낸 미쓰비시 PLC 래더 파일을 분석해서
  Unity HMI 프로젝트의 PLCConfig.json을 자동으로 업데이트하는 Skill.
  사용자가 PLC 래더 CSV 파일 경로를 제공하거나 래더가 변경되었다고 언급할 때
  반드시 이 Skill을 사용해야 한다.
  "래더 바뀌었어", "PLC 수정했어", "csv 파일", "PLCConfig 업데이트",
  "디바이스 주소 변경", "래더 분석해줘" 등의 표현이 나오면 즉시 이 Skill을 실행한다.
---

# PLC Ladder → PLCConfig.json 자동 동기화 Skill

## 개요

GX Works3 CSV 내보내기 파일을 파싱해서 디바이스 주소를 추출하고,
Unity HMI 프로젝트의 `PLCConfig.json` 을 자동으로 생성/업데이트한다.

**대상 프로젝트:** 스마트 손 씻기 디지털 트윈 HMI (SmartHandWashingDT)

---

## 실행 순서

### STEP 1 — CSV 파일 읽기

사용자가 제공한 경로의 CSV 파일을 읽는다.

```python
# 인코딩 주의: GX Works3 CSV는 UTF-16 LE (BOM 포함)
import csv, codecs

with codecs.open(csv_path, 'r', encoding='utf-16-le') as f:
    content = f.read()
# BOM 제거 후 파싱
```

CSV 컬럼 구조:
```
"Step No." | "Line Statement" | "Instruction" | "I/O (Device)" | "Blank" | "P/I Statement" | "Note"
   [0]            [1]               [2]               [3]           [4]          [5]            [6]
```

디바이스는 항상 **인덱스 3** 컬럼에 있다.

---

### STEP 2 — 디바이스 추출 및 분류

CSV에서 모든 디바이스를 추출하고 아래 규칙으로 분류한다.

**디바이스 분류표:**

| 접두어 | 종류 | 주소 체계 | 예시 |
|---|---|---|---|
| X | 입력 릴레이 | 16진수 | X0A0 = 0xA0 = 160 |
| Y | 출력 릴레이 | 16진수 | Y0 = 0x0 = 0 |
| M | 내부 릴레이 | 10진수 | M13 = 13 |
| D | 데이터 레지스터 | 10진수 | D0 = 0 |
| T | 타이머 | 10진수 | T1 = 1 |
| C | 카운터 | 10진수 | C0 = 0 |
| SM | 특수 릴레이 | 10진수 | SM402 |

**HMI 관련 디바이스 식별 규칙:**

아래 패턴으로 HMI 연동 디바이스를 자동 식별한다:

```
PLC → HMI 읽기 (출력 Y):
  - Y로 시작하는 OUT 명령 대상 → 실제 장비 출력 상태
  - Y0: 비누 실린더 전진 SOL
  - Y1: 비누 실린더 후진 SOL
  - Y2: 물 모터
  - Y3: 건조 모터

PLC → HMI 읽기 (내부 M):
  - SET/RST 명령으로 관리되는 M 비트
  - 자동 모드: M13 (SET/RST 쌍으로 찾기)
  - 수동 모드: M10 (SET/RST 쌍)
  - 비누 자동: M0, 물 자동: M4, 건조 자동: M5
  - 비누 수동: M7, 물 수동: M11, 건조 수동: M12

HMI → PLC 쓰기 (입력 X, ANDP 명령):
  - ANDP 명령 대상 X 디바이스 → HMI에서 써야 하는 신호
  - X0A0: 손 센서
  - X0A3~X0A5: 수동 PB 버튼들
```

---

### STEP 3 — 자동/수동 모드 구조 파악

CSV에서 아래 패턴을 찾아 모드 구조를 파악한다:

```
자동 모드 판단:
  LDI M10 (수동모드 NOT) → ANDP X0A0 → SET M13
  → M13 = 자동 모드 플래그

수동 모드 판단:
  LDI M13 (자동모드 NOT) → ANDP X0A3/X0A4/X0A5
  → 각 PB 입력 = 수동 모드 진입

동작 신호 식별:
  자동: M0(비누), M4(물), M5(건조) — SET 명령 기준
  수동: M7(비누), M11(물), M12(건조) — SET 명령 기준
  출력: Y0(비누전진), Y1(비누후진), Y2(물), Y3(건조)
```

---

### STEP 4 — NetworkManager 수정 필요 여부 판단

아래 조건을 확인해서 코드 수정 필요 여부를 판단한다:

```
코드 수정 불필요 (PLCConfig.json만 수정):
  ✅ X 디바이스 주소만 변경됨
  ✅ M/Y 번호만 변경됨
  ✅ D 레지스터 번호만 변경됨

코드 수정 필요:
  ⚠️ 자동/수동 모드 판단 방식 변경
     (M13 대신 다른 방식으로 모드 구분)
  ⚠️ 자동 모드에서 비누/물/건조 신호 디바이스 종류 변경
     (M → Y 또는 다른 디바이스로 변경)
  ⚠️ 완전히 새로운 시퀀스 단계 추가
```

---

### STEP 5 — PLCConfig.json 생성

추출된 디바이스로 PLCConfig.json을 생성한다.

**출력 경로:** `Assets/Resources/PLCConfig.json`

```json
{
  "useMock": true,
  "ip": "192.168.3.39",
  "port": 5007,
  "pollIntervalMs": 100,
  "timeoutMs": 2000,
  "devices": {
    "handSensor":         "[X0A0 주소]",
    "soapBtnManual":      "[X0A3 주소]",
    "waterBtnManual":     "[X0A4 주소]",
    "airBtnManual":       "[X0A5 주소]",
    "soapRunningAuto":    "[M0 주소]",
    "waterRunningAuto":   "[M4 주소]",
    "airRunningAuto":     "[M5 주소]",
    "soapRunningManual":  "[M7 주소]",
    "waterRunningManual": "[M11 주소]",
    "airRunningManual":   "[M12 주소]",
    "autoMode":           "[M13 주소]",
    "manualMode":         "[M10 주소]",
    "cycleEnd":           "[M6 주소]",
    "soapFwdOutput":      "[Y0 주소]",
    "waterOutput":        "[Y2 주소]",
    "airOutput":          "[Y3 주소]"
  },
  "soapDecreasePerUse": 5.0
}
```

> 기존 PLCConfig.json이 있으면 변경된 항목만 업데이트하고
> `useMock`, `ip`, `port` 등 운영 설정은 유지한다.

---

### STEP 6 — 변경 사항 리포트 출력

아래 형식으로 변경 사항을 출력한다:

```
=== PLC 래더 동기화 결과 ===

[디바이스 변경 사항]
변경됨:
  soapBtnManual: X0A3 → X1A3  (주소 변경)
  waterBtnManual: X0A4 → X1A4  (주소 변경)

유지됨:
  autoMode: M13 (변경 없음)
  soapRunningAuto: M0 (변경 없음)
  ...

[코드 수정 필요 여부]
  ✅ PLCConfig.json 수정만으로 완료
  또는
  ⚠️ NetworkManager.cs 수정 필요
     이유: [구체적인 이유]
     수정 위치: PollCoroutine() 내 [구체적인 라인]

[PLCConfig.json 저장 위치]
  Assets/Resources/PLCConfig.json

[다음 단계]
  1. Unity에서 PLCConfig.json 확인
  2. useMock: false로 변경 후 실제 PLC 연결 테스트
```

---

## 엣지 케이스 처리

### 인코딩 오류 시
```python
# UTF-16 실패 시 순서대로 시도
encodings = ['utf-16-le', 'utf-16', 'utf-8-sig', 'euc-kr', 'cp949']
```

### 디바이스가 기존 매핑에 없는 경우
- 새로운 X/M/Y 디바이스 발견 시 리포트에 표시
- "새로 추가된 디바이스: [목록]" 로 별도 섹션 출력
- 자동 매핑이 불확실한 경우 사용자에게 확인 요청

### 기존 PLCConfig.json이 없는 경우
- 새로 생성하되 `useMock: true`, `ip: "192.168.3.39"` 기본값 사용

### 자동/수동 모드가 없는 래더 (자동 전용)
- `manualMode`, `soapRunningManual` 등 수동 관련 키를 빈 문자열로 설정
- 리포트에 "수동 모드 없음 — 자동 모드 전용 래더" 표시

---

## 사용 예시

```
사용자: "래더 수정했어. csv 파일은 /path/to/ladder.csv 야"
→ 이 Skill 실행

사용자: "PLC 디바이스 주소 바뀌었는데 확인해줘"
→ CSV 경로 요청 후 이 Skill 실행

사용자: "ladder.csv 파일 분석해서 PLCConfig 업데이트해줘"
→ 이 Skill 실행
```

---

## 주의사항

1. **X/Y 디바이스는 반드시 16진수로 파싱** (X0A0 → 0xA0 = 160)
2. **기존 PLCConfig.json의 운영 설정 유지** (useMock, ip, port 덮어쓰기 금지)
3. **변경 없는 항목은 건드리지 않음**
4. **리포트는 항상 한국어로 출력**