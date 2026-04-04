# 작업 결과

### 1. `NetworkManager` 폴링 로직 수정 (인터록 및 타이머 문제 해결)
*   **문제 원인:** `StationController`에서 `TEST` 모드로 동작할 때 코루틴이 `isSoapRunning` 등을 `true`로 만들지만, `NetworkManager`의 `PollCoroutine`이 100ms마다 실행되면서 PLC(또는 MockPLC)에서 읽어온 상태(기본적으로 `false`)로 강제로 덮어씌워버렸습니다. 이 때문에 인터록 기능(`IsAnyRunning`)이 무력화되어 여러 버튼이 동시에 동작하고, 타이머 UI가 즉시 사라지는 문제가 있었습니다.
*   **수정 사항:** `NetworkManager.cs`의 `PollCoroutine` 내에서 현재 `AppModeManager.IsTestMode`인지 확인하도록 예외 처리를 추가했습니다.
*   **결과:** TEST 모드일 때는 PLC에서 읽어온 `bool` 비트 값을 `StationData`에 덮어쓰지 않고 무시합니다. 이제 코루틴이 관리하는 `true`/`false` 상태가 온전히 유지되므로, 비누/물/에어 중 하나가 동작 중일 때 다른 버튼을 눌러도 동작하지 않는 **인터록이 정상적으로 작동**하며, 설정된 시간 동안 **파티클 효과도 정상적으로 표시**됩니다.

### 2. `HMIUIController` 타이머 UI 로직 수정
*   **문제 원인:** 타이머 시작 로직이 이전 `Mock` 모드 코드 잔재를 유지하고 있었으며, `AppModeManager.IsTestMode` 조건을 제대로 확인하지 않아 타이머가 동작하지 않거나 오류가 발생했습니다.
*   **수정 사항:** `HMIUIController.cs`의 `RefreshSoapUI`, `RefreshWaterUI`, `RefreshAirUI` 함수 내부에서 `running && AppModeManager.IsTestMode` 일 때만 타이머 값을 초기화하도록 수정했습니다. `Update`문의 카운트다운 로직도 `AppModeManager.IsTestMode` 일때만 UI에 반영되도록 변경했습니다.
*   **결과:** 이제 TEST 모드에서만 비누, 물, 에어 버튼 클릭 시 타이머가 정상적으로 카운트다운 됩니다.

### 3. `MockPLCClient` 타이머 시뮬레이션 지원 (PLC 모드 테스트용)
*   `MockPLCClient.cs` 파일이 HMI에서 비트 쓰기 요청을 받았을 때 타이머에 의해 지정된 시간 이후 자동으로 꺼지도록 수정되었습니다. 이제 `PLC MODE`에서도 HMI 버튼을 클릭 시 설정된 시간 동안 `ON` 되었다가 자동으로 `OFF` 되는 **PLC의 자기유지 및 타이머 로직을 시뮬레이션** 할 수 있습니다. 
*   `PLCConfig.json`에서 `useMock`이 `true`로 설정된 경우, `PLC MODE` 상태에서도 실제 PLC 연결 없이 PLC 동작을 완벽하게 테스트할 수 있습니다.

### 4. `NetworkManager`에 센서 순차 동작 시뮬레이션 컨텍스트 메뉴 추가
*   `NetworkManager.cs` 파일 내부에 `MockSimulateSensorSequence` 컨텍스트 메뉴를 추가했습니다. 
*   `MockPLCClient.cs` 파일의 `SimulateSensorSequence` 메서드에서 비누 -> 물 -> 에어 순서대로 각각 설정된 시간만큼 동작하고 다음으로 넘어가는 **PLC 센서 입력에 의한 순차 동작** 로직을 시뮬레이션 하도록 구현했습니다.
*   **사용 방법**: Unity 에디터에서 `NetworkManager` 컴포넌트를 우클릭 한 후 `Mock: 센서 순차 동작 시뮬레이션`을 클릭하면 **PLC MODE**에서 실제 사용자가 손을 갖다 대었을 때 동작하는 센서 시퀀스를 테스트 할 수 있습니다.