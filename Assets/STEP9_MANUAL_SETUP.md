# STEP 9 — Unity 에디터 수동 설정 가이드

> **Claude Code로 생성된 스크립트 구현 완료 후**, 아래 항목들을 Unity 에디터에서 직접 설정해야 합니다.

---

## 1. 씬 오브젝트 설정

### 1-1. NetworkManager 컴포넌트 추가

1. **Hierarchy**에서 `StationManager` GameObject 선택
2. **Inspector** → **Add Component** → `NetworkManager` 검색 후 추가
3. `NetworkManager` 컴포넌트의 필드 설정:

| 필드 | 연결 대상 |
|------|-----------|
| **Station Data** | `Assets/Resources/StationDataInstance.asset` |

### 1-2. HMIUIController에 NetworkManager 연결

1. **Hierarchy**에서 `UIRoot` GameObject 선택
2. **Inspector**에서 `HMIUIController` 컴포넌트 찾기
3. `Network Manager` 필드 설정:

| 필드 | 연결 대상 |
|------|-----------|
| **Network Manager** | Hierarchy의 `StationManager` GameObject |

---

## 2. 동작 확인 — Mock 모드 테스트

### 2-1. 기본 연결 테스트

1. **Play 모드** 진입
2. **Console** 창에서 다음 로그 확인:
   ```
   [Network] 설정 로드: useMock=True, ip=192.168.1.10
   [MockPLC] 가상 PLC 연결됨
   [Network] 연결됨
   ```

### 2-2. 비누 사용 시뮬레이션

1. Play 모드 상태에서 **Hierarchy** → `StationManager` 선택
2. **Inspector**에서 `NetworkManager` 컴포넌트 찾기
3. 컴포넌트 헤더의 **⋮ (점 3개)** 메뉴 클릭
4. **"Mock: 비누 사용 시뮬레이션"** 클릭

**예상 결과:**
- 비누 게이지 5% 감소
- 20% 이하 시 헤더 LED 주황색
- 0% 시 헤더 LED 빨간색

### 2-3. 비누 리셋

1. 동일한 ContextMenu에서 **"Mock: 비누 잔량 리셋 (100%)"** 클릭
2. 게이지가 100%로 복구되는지 확인

---

## 3. Python 가상 서버 테스트 (선택)

실제 TCP 소켓 통신까지 검증하려면 Python 서버를 사용합니다.

### 3-1. Python 서버 실행

```bash
# 프로젝트 루트 폴더에서 실행
cd C:\Users\udune\Desktop\Unity\Projects\SmartHandWash
python fake_plc_server.py
```

**출력 예시:**
```
==================================================
  SLMP 가상 PLC 서버 시작
  주소: 127.0.0.1:5007
==================================================
초기 메모리:
  D0 (비누 잔량) = 1000 (100%)
  D10 (사용 횟수) = 0

Unity 연결 대기 중...
```

### 3-2. PLCConfig.json 수정

`Assets/Resources/PLCConfig.json` 파일 수정:

```json
{
  "useMock": false,        // ← true → false로 변경
  "ip": "127.0.0.1",       // ← 로컬호스트로 변경
  "port": 5007,
  ...
}
```

> ⚠️ **주의**: JSON 파일 수정 후 Unity 에디터에서 **Reimport** 필요
> 파일 우클릭 → Reimport

### 3-3. Unity Play 모드 진입

**Console 로그 확인:**
```
[Network] 설정 로드: useMock=False, ip=127.0.0.1
[SLMP] 연결 성공: 127.0.0.1:5007
[Network] 연결됨
```

**Python 서버 로그 확인:**
```
[연결됨] ('127.0.0.1', 54231)
  [읽기] D0 x1 → [1000]
  [읽기] M0 x3 → [0, 0, 0]
```

### 3-4. 테스트 완료 후 Mock 모드로 복원

```json
{
  "useMock": true,         // ← 다시 true로 변경
  "ip": "192.168.1.10",
  ...
}
```

---

## 4. 체크리스트

| # | 확인 항목 | 상태 |
|---|-----------|------|
| 1 | `StationManager`에 `NetworkManager` 컴포넌트 추가 | ☐ |
| 2 | `NetworkManager.stationData` 연결 | ☐ |
| 3 | `HMIUIController.networkManager` 연결 | ☐ |
| 4 | Mock 모드 연결 로그 확인 | ☐ |
| 5 | 비누 시뮬레이션 테스트 | ☐ |
| 6 | (선택) Python 서버 TCP 테스트 | ☐ |

---

## 5. 실제 PLC 연결 시 (향후)

PLC 도착 후 **코드 수정 없이** `PLCConfig.json`만 변경:

```json
{
  "useMock": false,
  "ip": "192.168.X.X",     // ← 실제 PLC IP
  "port": 5007,            // ← GX Works3에서 설정한 포트
  ...
}
```

### GX Works3 PLC 설정

1. **CPU 파라미터** → **Built-in Ethernet Port**
2. **SLMP 통신**: 활성화
3. **포트**: 5007
4. **통신 데이터 코드**: Binary

---

## 6. 트러블슈팅

| 문제 | 원인 | 해결책 |
|------|------|--------|
| `PLCConfig.json 없음` 로그 | Resources 폴더 위치 오류 | `Assets/Resources/PLCConfig.json` 경로 확인 |
| `SLMP 연결 타임아웃` | Python 서버 미실행 / IP 오류 | 서버 실행 확인, IP 주소 확인 |
| 비누 게이지 안 변함 | StationData 연결 안됨 | NetworkManager.stationData 연결 확인 |
| 버튼 클릭 시 PLC 쓰기 안됨 | NetworkManager 연결 안됨 | HMIUIController.networkManager 연결 확인 |

---

## 생성된 파일 목록

```
Assets/
├── Scripts/Network/
│   ├── IPLCClient.cs           ✅ 생성됨
│   ├── SLMPClient.cs           ✅ 생성됨
│   ├── MockPLCClient.cs        ✅ 생성됨
│   ├── NetworkManager.cs       ✅ 생성됨
│   └── PLCDeviceMap.cs         ✅ 생성됨
├── Resources/
│   └── PLCConfig.json          ✅ 생성됨
└── STEP9_MANUAL_SETUP.md       ✅ 이 파일

프로젝트 루트/
└── fake_plc_server.py          ✅ 생성됨 (Python 가상 서버)
```
