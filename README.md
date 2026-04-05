# SmartHandWashing

# 스마트 손 씻기 디지털 트윈 HMI
 
**Smart Hand Washing Station — Digital Twin HMI with PLC Integration**

### 한 줄 요약
 
> 미쓰비시 PLC와 SLMP TCP 통신으로 실시간 연동되는 스마트 손 씻기 스테이션의 Unity 기반 디지털 트윈 HMI 시스템
 
---
 
## 개발 목적
 
기존 산업 현장의 HMI는 전용 하드웨어 패널에 의존해 비용이 높고 유연성이 낮습니다. 본 프로젝트는 Unity UI Toolkit 기반의 소프트웨어 HMI와 3D 디지털 트윈을 결합해 다음 목표를 달성합니다.
 
- **실시간 상태 시각화** — PLC 신호를 100ms 주기로 폴링해 3D 모델과 UI에 즉시 반영
- **데이터 기반 관리** — 시간대별 비누 사용량 분석 및 교체 예측일 자동 계산
- **확장성** — 추후 API 서버 연동을 고려한 오프라인 우선 데이터 설계
- **교육적 활용** — 스마트 제조 환경에서 디지털 트윈의 실용적 구현 사례 제시
 
---
 
## 기술 스택
 
| 분류 | 기술 |
|---|---|
| 엔진 | Unity 6 (URP) |
| UI 시스템 | UI Toolkit (UXML + USS) |
| 3D 뷰어 | RenderTexture + Orbit Camera |
| PLC 통신 | SLMP 3E Frame TCP (C# System.Net.Sockets) |
| PLC 기종 | 미쓰비시 iQ-R 시리즈 (R02CPU) |
| 데이터 저장 | JSON (Application.persistentDataPath) |
| 언어 | C# |
| 개발 도구 | Unity Editor, GX Works3, GX Simulator3 |

### 핵심 설계 원칙
 
**인터페이스 기반 통신 분리**
`IPLCClient` 인터페이스로 실제 PLC(`SLMPClient`)와 가상 PLC(`MockPLCClient`)를 분리해 PLC 없이도 전체 시스템 동작 및 테스트가 가능하도록 설계했습니다.
 
**오프라인 우선 데이터 설계**
모든 이벤트 데이터를 로컬 JSON 큐에 우선 저장하고, 추후 API 서버 연결 시 자동 배치 전송하는 구조로 설계해 서버 없이도 완전히 동작합니다.
 
**설정 외부화**
IP 주소, 디바이스 주소, 예측 임계값 등 모든 환경 설정을 JSON 파일로 분리해 코드 수정 없이 운영 환경 전환이 가능합니다.
 
---

## PLC 연동 설계
 
### SLMP 3E Frame 구현
 
외부 라이브러리 없이 C# `System.Net.Sockets`로 SLMP Binary 3E Frame을 구현했습니다.
 
```csharp
// SLMP 3E Frame 구조
[서브헤더 0x50 0x00] [네트워크번호] [PC번호]
[요청처I/O] [스테이션번호] [데이터길이]
[CPU타이머] [명령어] [서브명령] [디바이스번호] [점수]
```
 
**구현한 기능:**
- 워드 읽기 (D 디바이스) — 비누 잔량
- 비트 읽기 (M, X 디바이스) — 동작 상태 신호
- 비트 쓰기 — HMI 버튼 → PLC 신호 전달
- 응답 파싱 및 오류 코드 처리
- 연결 실패 시 5초 후 자동 재연결

 ### 테스트 환경 구성
 
PLC 없이도 전체 시스템을 검증할 수 있도록 3단계 테스트 환경을 구성했습니다.
 
```
단계 1: Mock 모드
  Unity 내부 MockPLCClient로 가상 데이터 생성
  → UI/파티클/그래프 동작 검증
 
단계 2: Python 가상 PLC 서버
  SLMP 응답을 흉내내는 Python TCP 서버
  → 실제 TCP 소켓 통신 및 패킷 파싱 검증
 
단계 3: GX Simulator3 연동
  GX Works3 내장 PLC 소프트웨어 에뮬레이터
  → 실제 래더 로직과 함께 전체 인터록 검증
 
단계 4: 실제 PLC 연결
  PLCConfig.json IP만 변경 → 코드 수정 없음
```

## 개발 과정 및 의사결정
 
### 1. UI 시스템 선택 — UI Toolkit vs uGUI
 
uGUI(Canvas 기반) 대신 UI Toolkit을 선택했습니다.
 
| 항목 | uGUI | UI Toolkit (선택) |
|---|---|---|
| 스타일링 | Inspector 수동 설정 | USS (CSS 유사) 파일로 일괄 관리 |
| 레이아웃 | Anchor/Pivot 수동 | Flexbox 자동 |
| HMI 적합성 | 보통 | 높음 (산업 UI에 적합한 구조) |
| 그래프 구현 | 외부 에셋 필요 | painter2D 내장 API 활용 |
 
### 2. PLC 통신 프로토콜 선택 — SLMP vs Modbus TCP vs OPC-UA
 
| 프로토콜 | 검토 결과 |
|---|---|
| **SLMP** | 미쓰비시 기본 내장, 추가 모듈 불필요, C# 직접 구현 가능 → **채택** |
| Modbus TCP | 범용성 높으나 iQ-R에서 별도 모듈 필요 |
| OPC-UA | 대규모 시스템에 적합하나 Unity 호환성 복잡, 과도한 구성 비용 |
 
### 3. 동작 방식 설계 — 고정 타이머 → PLC 신호 기반
 
초기 구현은 버튼 클릭 시 고정 시간(3초/10초) 동안 동작하는 방식이었으나, 실제 산업 HMI에서는 PLC가 장비를 제어하고 HMI는 상태를 미러링하는 구조가 올바름을 파악해 전면 수정했습니다.
 
```
[수정 전] 버튼 클릭 → HMI가 직접 타이머로 제어
[수정 후] 버튼 클릭 → PLC에 신호 쓰기 → PLC 신호 폴링 → 상태 반영
```
 
이 변경으로 PLC 래더의 인터록 로직과 HMI가 자연스럽게 연동되었습니다.
 
### 4. 다층 데이터 설계 — 실제 연동 vs 샘플 데이터
 
8개 층 전체에 PLC 연동을 구현하는 대신, 실제 운영 층(8F)만 PLC 연동하고 나머지는 샘플 데이터로 데모하는 현실적인 설계를 채택했습니다.
 
```
8F: 실제 PLC 연동 (isRealPLC: true)
1F~7F: FloorSampleData.json 샘플 데이터
```
 
`FloorManager.IsRealPLCFloor` 플래그 하나로 전체 시스템의 PLC 신호 처리 여부를 제어해 코드 복잡도를 최소화했습니다.
 
---

### 기술적 성과
 
- 외부 UI 라이브러리 없이 UI Toolkit `painter2D`로 실시간 막대 그래프 구현
- 외부 PLC 통신 라이브러리 없이 SLMP Binary 3E Frame 완전 구현
- `IPLCClient` 인터페이스로 Mock ↔ 실제 PLC 전환 시 코드 수정 없음
- `PLCConfig.json` IP 변경만으로 시뮬레이터 → 실제 PLC 전환 가능

## 향후 계획
 
- [ ] 미쓰비시 R02CPU 실물 PLC 연결 및 SLMP 통신 검증
- [ ] Spring 기반 REST API 서버 구축 및 데이터 동기화
- [ ] PostgreSQL DB 연동 및 다중 스테이션 관제 대시보드
- [ ] Windows Standalone 빌드 및 HMI PC 배포
| 버전 관리 | Git |
 
---
