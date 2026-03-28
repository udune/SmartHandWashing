// UnityEngine: ScriptableObject, Mathf, Range 등 Unity 핵심 API
using UnityEngine;
// System: Action 델리게이트 타입 (이벤트 콜백용)
using System;

// [CreateAssetMenu]: Assets 우클릭 → Create → SmartWash → Station Data로 인스턴스 생성 가능
[CreateAssetMenu(menuName = "SmartWash/Station Data")]
// ScriptableObject: 씬과 독립적인 데이터 에셋 (.asset 파일로 저장)
// MVC 패턴의 Model 역할
public class StationData : ScriptableObject
{
    // [Header]: Inspector에서 필드 그룹 제목 표시
    [Header("Soap")]
    // [Range]: Inspector에서 슬라이더 표시, 값 범위 0~100 제한
    [Range(0f, 100f)]
    // 비누 잔량 (0~100%), NetworkManager가 PLC에서 읽은 값으로 갱신
    public float soapLevel = 100f;

    // 누적 사용 횟수
    public int soapUseCount = 0;
    // 1회 사용당 감소량 (기본 5%)
    public float soapDecreasePerUse = 5f;

    [Header("Analytics")]
    // 시간대별 누적 (런타임 캐시용) - SoapUsageLogger와 동기화
    public int[] hourlyUsageCount = new int[24];

    [Header("Running State")]
    // 각 디스펜서의 현재 작동 여부 (인터락 체크 및 UI LED 상태에 사용)
    public bool isSoapRunning = false;
    public bool isWaterRunning = false;
    public bool isAirRunning  = false;

    // 시스템 상태 열거형: Normal(정상), Warning(비누 20% 이하), Error(비누 0%)
    public enum SystemStatus
    {
        Normal,
        Warning,
        Error
    }

    // 현재 시스템 상태 (UI 헤더 LED 색상 결정에 사용)
    public SystemStatus systemStatus = SystemStatus.Normal;

    // event Action: 데이터 변경 시 구독자에게 알림 (옵저버 패턴)
    public event Action OnDataChanged;

    // 비누 사용 처리 (주의: PLC Master 모드에서는 NetworkManager가 soapLevel을 직접 갱신)
    public void UseSoap()
    {
        // Mathf.Max(0f, ...): 음수 방지 (최소값 0)
        soapLevel = Mathf.Max(0f, soapLevel - soapDecreasePerUse);
        soapUseCount++;

        // 비누 잔량에 따른 시스템 상태 자동 전환
        if (soapLevel <= 20f && soapLevel > 0f)
        {
            systemStatus = SystemStatus.Warning;
        }

        if (soapLevel <= 0f)
        {
            systemStatus = SystemStatus.Error;
        }

        // null-conditional: 구독자 없을 때 안전하게 호출
        OnDataChanged?.Invoke();
    }

    // 모든 필드를 초기값으로 리셋 (테스트 또는 시스템 재시작 시 사용)
    public void ResetData()
    {
        soapLevel = 100f;
        soapUseCount = 0;
        isSoapRunning = false;
        isWaterRunning = false;
        isAirRunning = false;
        systemStatus = SystemStatus.Normal;
        OnDataChanged?.Invoke();  // UI 갱신 트리거
    }
}
