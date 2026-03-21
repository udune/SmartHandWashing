// UnityEngine: MonoBehaviour, ParticleSystem, Coroutine 등 Unity 핵심 API
using UnityEngine;
// System: Action 델리게이트 타입 (이벤트 콜백용)
using System;
// System.Collections: IEnumerator 인터페이스 (코루틴 반환 타입)
using System.Collections;

// 손 세정 스테이션의 3가지 디스펜서(비누, 물, 에어)를 제어하는 컨트롤러
public class StationController : MonoBehaviour
{
    // [Header]: Inspector에서 필드를 그룹화하여 표시
    [Header("Data")]
    // StationData: ScriptableObject 참조 (MVC의 Model 역할)
    public StationData stationData;

    [Header("Particles")]
    // ParticleSystem: 각 디스펜서 작동 시 재생할 파티클 이펙트 (씬의 ParticleRoot 하위)
    public ParticleSystem soapParticle;
    public ParticleSystem waterParticle;
    public ParticleSystem airParticle;

    // event Action: 옵저버 패턴 구현 - HMIUIController가 구독하여 UI 갱신
    public event Action OnSoapUpdated;
    public event Action OnWaterUpdated;
    public event Action OnAirUpdated;

    // Coroutine 핸들: 중복 실행 방지 및 StopCoroutine() 호출에 사용
    private Coroutine _soapCoroutine;
    private Coroutine _waterCoroutine;
    private Coroutine _airCoroutine;

    // Awake(): Start()보다 먼저 호출되는 초기화 콜백
    void Awake()
    {
        // Play Mode 시작 시 상태 초기화 (ScriptableObject 값 유지 문제 방지)
        stationData.isSoapRunning = false;
        stationData.isWaterRunning = false;
        stationData.isAirRunning = false;
    }

    /// <summary>
    /// 인터락 체크: 어떤 dispenser든 실행 중이면 true
    /// </summary>
    private bool IsAnyRunning()
    {
        if (stationData.isSoapRunning)
        {
            return true;
        }
        if (stationData.isWaterRunning)
        {
            return true;
        }
        if (stationData.isAirRunning)
        {
            return true;
        }
        return false;
    }

    // 비누 디스펜서 활성화 (3초 작동, 잔량 소모)
    public void ActivateSoap()
    {
        // 인터락: 다른 동작 중이면 실행 불가
        if (IsAnyRunning() || stationData.soapLevel <= 0f)
        {
            return;
        }

        // 기존 코루틴 정지 후 새로 시작 (중복 실행 방지)
        if (_soapCoroutine != null)
        {
            StopCoroutine(_soapCoroutine);
        }

        // StartCoroutine(): 코루틴 시작하고 핸들 반환
        _soapCoroutine = StartCoroutine(RunDispenser(
            // Lambda 표현식: v => 로 상태 변경 로직을 콜백으로 전달
            setter: v => stationData.isSoapRunning = v,
            duration: 3f,
            particle: soapParticle,
            // 비누 잔량 감소는 PLC(Mock)에서 처리 → NetworkManager 폴링으로 동기화
            onStart: () => OnSoapUpdated?.Invoke(),
            onEnd:   () => OnSoapUpdated?.Invoke()
        ));
    }

    // 물 디스펜서 활성화 (10초 작동, 잔량 무제한)
    public void ActivateWater()
    {
        // 인터락: 다른 동작 중이면 실행 불가
        if (IsAnyRunning())
        {
            return;
        }

        if (_waterCoroutine != null)
        {
            StopCoroutine(_waterCoroutine);
        }

        _waterCoroutine = StartCoroutine(RunDispenser(
            setter: v => stationData.isWaterRunning = v,
            duration: 10f,
            particle: waterParticle,
            onStart: () => OnWaterUpdated?.Invoke(),
            onEnd:   () => OnWaterUpdated?.Invoke()
        ));
    }

    // 에어 드라이어 활성화 (10초 작동, 잔량 무제한)
    public void ActivateAir()
    {
        // 인터락: 다른 동작 중이면 실행 불가
        if (IsAnyRunning())
        {
            return;
        }

        if (_airCoroutine != null)
        {
            StopCoroutine(_airCoroutine);
        }

        _airCoroutine = StartCoroutine(RunDispenser(
            setter: v => stationData.isAirRunning = v,
            duration: 10f,
            particle: airParticle,
            onStart: () => OnAirUpdated?.Invoke(),
            onEnd:   () => OnAirUpdated?.Invoke()
        ));
    }

    // 제네릭 디스펜서 코루틴: Action 파라미터로 비누/물/에어 공통 처리
    // IEnumerator: 코루틴 반환 타입
    private IEnumerator RunDispenser(
        Action<bool> setter, float duration,
        ParticleSystem particle,
        Action onStart, Action onEnd)
    {
        setter(true);  // 상태 ON
        if (particle != null)
        {
            particle.gameObject.SetActive(true);  // 파티클 오브젝트 활성화
            particle.Play();  // 파티클 재생 시작
        }
        onStart?.Invoke();  // null-conditional: 구독자 없을 때 안전하게 호출

        float elapsed = 0f;  // 경과 시간 추적
        while (elapsed < duration)  // 타이머 루프
        {
            elapsed += Time.deltaTime;  // Time.deltaTime: 이전 프레임과의 시간 차이 (초)
            yield return null;  // 1프레임 대기 (Update와 동기화)
        }

        setter(false);  // 상태 OFF
        if (particle != null)
        {
            particle.Stop();  // 파티클 재생 정지
        }
        onEnd?.Invoke();  // 종료 이벤트 발행
    }

    // 타이머 남은 시간 조회용 (UI에서 폴링)
    // UI 폴링용: 비누 남은 시간 (주의: 현재는 전체 시간만 반환)
    public float GetSoapRemaining()
    {
        return GetRemaining(_soapCoroutine, 3f);
    }

    // UI 폴링용: 물 남은 시간
    public float GetWaterRemaining()
    {
        return GetRemaining(_waterCoroutine, 10f);
    }

    // UI 폴링용: 에어 남은 시간
    public float GetAirRemaining()
    {
        return GetRemaining(_airCoroutine, 10f);
    }

    // 코루틴 존재 여부로 실행 상태 판단 (한계: 실제 남은 시간 미반영)
    private float GetRemaining(Coroutine c, float total)
    {
        if (c != null)
        {
            return total;  // 코루틴 실행 중이면 전체 시간 반환
        }
        return 0f;  // 실행 중 아니면 0 반환
    }
}
