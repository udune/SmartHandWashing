using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// PLC 신호(isSoapRunning / isWaterRunning / isAirRunning) 를 읽어
/// 파티클과 UI 상태를 반영하는 컨트롤러.
///
/// 동작 규칙:
///   - PLC 모드: NetworkManager 폴링이 신호를 갱신 → 이 클래스가 감지해 파티클 On/Off
///   - TEST 모드: 버튼 클릭 시 코루틴으로 타이머 동작 (AppModeManager로 전환)
///   - 1F~7F 선택 중에는 FloorManager.IsRealPLCFloor = false → 모든 신호 무시
/// </summary>
public class StationController : MonoBehaviour
{
    [Header("Data")]
    public StationData stationData;

    [Header("Particles")]
    public ParticleSystem soapParticle;
    public ParticleSystem waterParticle;
    public ParticleSystem airParticle;

    [Header("Timing (TEST Mode)")]
    [Tooltip("TEST 모드에서 비누 동작 시간 (초)")]
    public float soapDuration = 5f;
    [Tooltip("TEST 모드에서 물 동작 시간 (초)")]
    public float waterDuration = 8f;
    [Tooltip("TEST 모드에서 에어 동작 시간 (초)")]
    public float airDuration = 8f;

    private bool _prevSoap;
    private bool _prevWater;
    private bool _prevAir;

    private Coroutine _soapCoroutine;
    private Coroutine _waterCoroutine;
    private Coroutine _airCoroutine;

    public event Action OnSoapUpdated;
    public event Action OnWaterUpdated;
    public event Action OnAirUpdated;

    void Awake()
    {
        stationData.isSoapRunning = false;
        stationData.isWaterRunning = false;
        stationData.isAirRunning = false;
    }

    void Update()
    {
        bool plcActive = FloorManager.Instance == null
                         || FloorManager.Instance.IsRealPLCFloor;

        if (!plcActive)
        {
            ForceStopAll();
            return;
        }

        // TEST 모드에서는 코루틴이 상태를 관리하므로 PLC 신호 체크 스킵
        if (AppModeManager.IsTestMode)
        {
            return;
        }

        CheckSignal(stationData.isSoapRunning, ref _prevSoap, soapParticle, OnSoapUpdated);
        CheckSignal(stationData.isWaterRunning, ref _prevWater, waterParticle, OnWaterUpdated);
        CheckSignal(stationData.isAirRunning, ref _prevAir, airParticle, OnAirUpdated);
    }

    private void CheckSignal(bool current, ref bool prev, ParticleSystem particle, Action onChanged)
    {
        if (current == prev)
        {
            return;
        }

        prev = current;

        if (particle != null)
        {
            if (current)
            {
                particle.gameObject.SetActive(true);
                particle.Play();
            }
            else
            {
                particle.Stop();
            }
        }

        onChanged?.Invoke();
    }

    /// <summary>
    /// 비누/물/에어 중 하나라도 동작 중이면 true.
    /// PLC 모드·TEST 모드 공통으로 사용하는 인터록 조건.
    /// </summary>
    private bool IsAnyRunning =>
        stationData.isSoapRunning ||
        stationData.isWaterRunning ||
        stationData.isAirRunning;

    public void RequestSoap()
    {
        if (FloorManager.Instance != null && !FloorManager.Instance.IsRealPLCFloor)
        {
            return;
        }

        if (IsAnyRunning)
        {
            return;
        }

        if (stationData.soapLevel <= 0f)
        {
            return;
        }

        if (AppModeManager.IsTestMode)
        {
            ActivateSoap();
            return;
        }

        // PLC 모드: PLC에 신호 쓰기
        NetworkManager.Instance?.WriteSoapButton(true);
    }

    public void RequestWater()
    {
        if (FloorManager.Instance != null && !FloorManager.Instance.IsRealPLCFloor)
        {
            return;
        }

        if (IsAnyRunning)
        {
            return;
        }

        if (AppModeManager.IsTestMode)
        {
            ActivateWater();
            return;
        }

        NetworkManager.Instance?.WriteWaterButton(true);
    }

    public void RequestAir()
    {
        if (FloorManager.Instance != null && !FloorManager.Instance.IsRealPLCFloor)
        {
            return;
        }

        if (IsAnyRunning)
        {
            return;
        }

        if (AppModeManager.IsTestMode)
        {
            ActivateAir();
            return;
        }

        NetworkManager.Instance?.WriteAirButton(true);
    }

    private void ActivateSoap()
    {
        if (_soapCoroutine != null)
        {
            StopCoroutine(_soapCoroutine);
        }

        // 인터록: 코루틴 시작 전에 즉시 running 상태 설정
        stationData.isSoapRunning = true;
        _prevSoap = true;

        float levelBefore = stationData.soapLevel;
        stationData.UseSoap();

        // 파티클 즉시 시작
        if (soapParticle != null)
        {
            soapParticle.gameObject.SetActive(true);
            soapParticle.Play();
        }

        SoapUsageLogger.Instance?.LogSoap(levelBefore, stationData.soapLevel);
        OnSoapUpdated?.Invoke();

        _soapCoroutine = StartCoroutine(DispenserTimer(soapDuration, () =>
        {
            stationData.isSoapRunning = false;
            _prevSoap = false;
            soapParticle?.Stop();
            OnSoapUpdated?.Invoke();
        }));
    }

    private void ActivateWater()
    {
        if (_waterCoroutine != null)
        {
            StopCoroutine(_waterCoroutine);
        }

        stationData.isWaterRunning = true;
        _prevWater = true;

        if (waterParticle != null)
        {
            waterParticle.gameObject.SetActive(true);
            waterParticle.Play();
        }

        SoapUsageLogger.Instance?.LogWater();
        OnWaterUpdated?.Invoke();

        _waterCoroutine = StartCoroutine(DispenserTimer(waterDuration, () =>
        {
            stationData.isWaterRunning = false;
            _prevWater = false;
            waterParticle?.Stop();
            OnWaterUpdated?.Invoke();
        }));
    }

    private void ActivateAir()
    {
        if (_airCoroutine != null)
        {
            StopCoroutine(_airCoroutine);
        }

        stationData.isAirRunning = true;
        _prevAir = true;

        if (airParticle != null)
        {
            airParticle.gameObject.SetActive(true);
            airParticle.Play();
        }

        SoapUsageLogger.Instance?.LogAir();
        OnAirUpdated?.Invoke();

        _airCoroutine = StartCoroutine(DispenserTimer(airDuration, () =>
        {
            stationData.isAirRunning = false;
            _prevAir = false;
            airParticle?.Stop();
            OnAirUpdated?.Invoke();
        }));
    }

    /// <summary>지정된 시간 후 콜백 실행 (TEST 모드 타이머)</summary>
    private IEnumerator DispenserTimer(float duration, Action onEnd)
    {
        yield return new WaitForSeconds(duration);
        onEnd?.Invoke();
    }

    /// <summary>1F~7F 전환 시 모든 동작 강제 중단</summary>
    private void ForceStopAll()
    {
        if (stationData.isSoapRunning || stationData.isWaterRunning || stationData.isAirRunning)
        {
            if (_soapCoroutine != null) { StopCoroutine(_soapCoroutine); _soapCoroutine = null; }
            if (_waterCoroutine != null) { StopCoroutine(_waterCoroutine); _waterCoroutine = null; }
            if (_airCoroutine != null) { StopCoroutine(_airCoroutine); _airCoroutine = null; }

            stationData.isSoapRunning = false;
            stationData.isWaterRunning = false;
            stationData.isAirRunning = false;
            _prevSoap = false;
            _prevWater = false;
            _prevAir = false;

            soapParticle?.Stop();
            waterParticle?.Stop();
            airParticle?.Stop();

            OnSoapUpdated?.Invoke();
            OnWaterUpdated?.Invoke();
            OnAirUpdated?.Invoke();
        }
    }
}
