using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// PLC 래더 로직 시뮬레이터 + 3D 시각화 컨트롤러.
///
/// TEST 모드: 내부에서 미쓰비시 PLC 래더 로직 완전 재현
///            (SmartWash_PLC_Logic_Unity_Spec.md §5)
/// PLC  모드: NetworkManager 폴링 결과(stationData)를 파티클/애니메이션에 반영
/// </summary>
public class StationController : MonoBehaviour
{
    [Header("Data")]
    public StationData stationData;

    [Header("Particles")]
    public ParticleSystem waterParticle;   // Y0C0: 물
    public ParticleSystem soapParticle;    // Y0C1: 세정제 전진(분사) 이펙트
    public ParticleSystem airParticle;     // Y0C3: 바람

    [Header("Cylinder Animation (Optional)")]
    public Animator soapCylinderAnimator;  // 세정제 실린더 Animator

    [Header("TEST Mode — Cylinder Travel Simulation")]
    [Tooltip("전진/후진 완료 센서 응답 시뮬레이션 시간(초)")]
    public float cylinderTravelTime = 0.5f;

    [Header("Timer Durations (래더 스펙 고정값)")]
    [Tooltip("세정제: 센서 기반 — 고정 타이머 없음")]
    public float soapDuration  = 0f;
    [Tooltip("물: T1/T3 = 10초")]
    public float waterDuration = 10f;
    [Tooltip("바람: T2/T4 = 10초")]
    public float airDuration   = 10f;

    // ── HMI 이벤트 ──────────────────────────────────────────────────
    public event Action OnSoapUpdated;
    public event Action OnWaterUpdated;
    public event Action OnAirUpdated;

    // ════════════════════════════════════════════════════════════════
    //  TEST 모드 — PLC 래더 상태 변수 (§5.1)
    // ════════════════════════════════════════════════════════════════

    // 모드 선택 릴레이
    private bool _M10, _M20, _M30, _M40;

    // 수동 동작 릴레이
    private bool _M0, _M1, _M2, _M3;

    // 자동 동작 릴레이
    private bool _M4, _M5, _M6, _M7;

    // 타이머 (OUT T 방식)
    private float _t0, _t1, _t2, _t3, _t4;
    private bool  _T0, _T1, _T2, _T3, _T4;

    // 카운터
    private int  _C0val;
    private bool _C0;

    // 출력
    private bool _Y0C0, _Y0C1, _Y0C2, _Y0C3;

    // 입력 시뮬레이션
    private bool _X0A0, _X0A1;                      // 센서 — 레벨 유지
    private bool _X0A6, _X0A7, _X0A8, _X0A9;       // 버튼/손 센서 — 1프레임 펄스

    // 이전 프레임 값 (엣지 검출용 — §규칙 3)
    private bool _prevX0A6, _prevX0A7, _prevX0A8, _prevX0A9;
    private bool _prevM0,  _prevM2,  _prevM3,  _prevM5,  _prevM7;
    private bool _prevT0,  _prevT1,  _prevT2,  _prevT3,  _prevT4;
    private bool _prevC0;

    // 시각화 변화 감지
    private bool _prevY0C0, _prevY0C1, _prevY0C2, _prevY0C3, _prevSoapActive;

    // 실린더 이동 시뮬레이션
    private float _fwdTimer = -1f;   // 전진 이동 타이머 (-1 = 비활성)
    private float _bwdTimer = -1f;   // 후진 이동 타이머

    // PLC 모드 미러링 이전값
    private bool _mirrorSoap, _mirrorWater, _mirrorAir, _mirrorFwd, _mirrorBwd;

    private bool _prevPlcActive = true;

    // ════════════════════════════════════════════════════════════════
    //  생명주기
    // ════════════════════════════════════════════════════════════════

    void Awake()
    {
        // 초기 위치: 실린더 후진 완료 (X0A0 = true)
        _X0A0 = true;
        _X0A1 = false;

        stationData.isSoapRunning   = false;
        stationData.isWaterRunning  = false;
        stationData.isAirRunning    = false;
        stationData.isSoapForward   = false;
        stationData.isSoapBackward  = false;
    }

    void Update()
    {
        bool plcActive = FloorManager.Instance == null || FloorManager.Instance.IsRealPLCFloor;
        if (!plcActive)
        {
            if (_prevPlcActive)
            {
                ForceStopAll();
            }
            _prevPlcActive = false;
            return;
        }
        _prevPlcActive = true;

        if (AppModeManager.IsTestMode)
        {
            SimulateLadder();
        }
        else
        {
            MirrorPLCState();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  TEST 모드 — 래더 로직 시뮬레이션
    // ════════════════════════════════════════════════════════════════

    /// <summary>실린더 이동 시뮬레이션 → X0A0(후진 완료), X0A1(전진 완료) 갱신</summary>
    private void SimulateCylinderSensors()
    {
        bool fwdRunning = _M1 || _M4;
        bool bwdRunning = _M2 || _M5;

        // 전진 시작 감지
        if (fwdRunning && _fwdTimer < 0f)
        {
            _fwdTimer = 0f;
            _X0A0 = false;   // 후진 위치 이탈
        }
        // 후진 시작 감지
        if (bwdRunning && _bwdTimer < 0f)
        {
            _bwdTimer = 0f;
            _X0A1 = false;   // 전진 위치 이탈
        }

        // 전진 이동 경과
        if (_fwdTimer >= 0f)
        {
            if (fwdRunning)
            {
                _fwdTimer += Time.deltaTime;
                if (_fwdTimer >= cylinderTravelTime)
                {
                    _X0A1 = true;
                    _fwdTimer = -1f;
                }
            }
            else
            {
                _fwdTimer = -1f;
            }
        }

        // 후진 이동 경과
        if (_bwdTimer >= 0f)
        {
            if (bwdRunning)
            {
                _bwdTimer += Time.deltaTime;
                if (_bwdTimer >= cylinderTravelTime)
                {
                    _X0A0 = true;
                    _bwdTimer = -1f;
                }
            }
            else
            {
                _bwdTimer = -1f;
            }
        }
    }

    /// <summary>§5.4 메인 업데이트 로직 — 11단계 순서 엄수</summary>
    private void SimulateLadder()
    {
        // 실린더 센서 시뮬레이션 (X0A0, X0A1 갱신)
        SimulateCylinderSensors();

        // ── 1단계: 타이머 업데이트 ────────────────────────────────
        UpdateTimer(_M10 || _M20 || _M30 || _M40, ref _t0, ref _T0, 2.0f);
        UpdateTimer(_M0,  ref _t1, ref _T1, waterDuration);
        UpdateTimer(_M3,  ref _t2, ref _T2, airDuration);
        UpdateTimer(_M6,  ref _t3, ref _T3, waterDuration);
        UpdateTimer(_M7,  ref _t4, ref _T4, airDuration);

        // ── 2단계: 모드 선택 SET ──────────────────────────────────
        if (Rise(_X0A7, _prevX0A7) && !_M20 && !_M30 && !_M40)
        {
            _M10 = true;
        }
        if (Rise(_X0A8, _prevX0A8) && !_M10 && !_M30 && !_M40 && !_X0A0)
        {
            _M20 = true;
        }
        if (Rise(_X0A9, _prevX0A9) && !_M10 && !_M20 && !_M40)
        {
            _M30 = true;
        }
        if (Rise(_X0A6, _prevX0A6) && !_M10 && !_M20 && !_M30 && _X0A0)
        {
            _M40 = true;
        }

        // ── 3단계: 수동 물 ────────────────────────────────────────
        if (Rise(_T0, _prevT0) && !_M20 && !_M30 && !_M40)
        {
            _M0 = true;
        }
        if (Rise(_T1, _prevT1))
        {
            _M0 = false;
        }

        // ── 4단계: 수동 세정제 ────────────────────────────────────
        if (Rise(_T0, _prevT0) && !_M10 && !_M30 && !_M40)
        {
            _M1 = true;
        }
        if (_M1 && _X0A1)
        {
            _M1 = false;
            _M2 = true;
        }
        if (_M2 && _X0A0)
        {
            _M2 = false;
        }

        // ── 5단계: 수동 바람 ──────────────────────────────────────
        if (Rise(_T0, _prevT0) && !_M10 && !_M20 && !_M40)
        {
            _M3 = true;
        }
        if (Rise(_T2, _prevT2))
        {
            _M3 = false;
        }

        // ── 6단계: 자동 세정제 왕복 ──────────────────────────────
        // 첫 전진: T0↑ + 자동 모드만 활성 + 카운터 미완료
        // 재전진: M5 하강엣지 + 카운터 미완료 (§규칙 4: C0=true이면 재SET 금지)
        if ((Rise(_T0, _prevT0) && !_M10 && !_M20 && !_M30 && !_C0)
            || (Fall(_M5, _prevM5) && !_C0))
        {
            _M4 = true;
        }

        if (_M4 && _X0A1)
        {
            _M4 = false;
            _M5 = true;
        }
        if (_M5 && _X0A0)
        {
            _C0val++;
            _C0 = _C0val >= 2;
            _M5 = false;
        }

        // ── 7단계: 자동 물 ────────────────────────────────────────
        if (Rise(_C0, _prevC0))
        {
            _M6 = true;
        }
        if (Rise(_T3, _prevT3))
        {
            _M6 = false;
            _M7 = true;
        }

        // ── 8단계: 자동 바람 ──────────────────────────────────────
        if (Rise(_T4, _prevT4))
        {
            _M7 = false;
        }

        // ── 9단계: 모드 리셋 (하강엣지) ──────────────────────────
        if (Fall(_M0, _prevM0))
        {
            _M10 = false;
        }
        if (Fall(_M2, _prevM2))
        {
            _M20 = false;
        }
        if (Fall(_M3, _prevM3))
        {
            _M30 = false;
        }
        if (Fall(_M7, _prevM7))
        {
            _M40 = false;
            _C0val = 0;
            _C0 = false;
        }

        // ── 10단계: 출력 매핑 ─────────────────────────────────────
        _Y0C0 = _M0 || _M6;   // 물 밸브
        _Y0C1 = _M1 || _M4;   // 세정제 전진 솔레노이드
        _Y0C2 = _M2 || _M5;   // 세정제 후진 솔레노이드
        _Y0C3 = _M3 || _M7;   // 에어 블로워

        // ── 11단계: 이전 프레임 저장 ──────────────────────────────
        _prevX0A6 = _X0A6;
        _prevX0A7 = _X0A7;
        _prevX0A8 = _X0A8;
        _prevX0A9 = _X0A9;
        _prevM0 = _M0;
        _prevM2 = _M2;
        _prevM3 = _M3;
        _prevM5 = _M5;
        _prevM7 = _M7;
        _prevT0 = _T0;
        _prevT1 = _T1;
        _prevT2 = _T2;
        _prevT3 = _T3;
        _prevT4 = _T4;
        _prevC0 = _C0;

        // ── 펄스 입력 클리어 (버튼: 1프레임 펄스) ─────────────────
        _X0A6 = false;
        _X0A7 = false;
        _X0A8 = false;
        _X0A9 = false;

        ApplyLadderOutputs();
    }

    /// <summary>Y 출력 변화 → 파티클 / 애니메이션 / StationData 반영</summary>
    private void ApplyLadderOutputs()
    {
        bool soapActive = _Y0C1 || _Y0C2;

        // 세정제 전진 시작 → 파티클 ON + 비누 소모
        if (_Y0C1 && !_prevY0C1)
        {
            soapParticle?.gameObject.SetActive(true);
            soapParticle?.Play();
            soapCylinderAnimator?.SetTrigger("SoapForward");
            float before = stationData.soapLevel;
            stationData.UseSoap();
            SoapUsageLogger.Instance?.LogSoap(before, stationData.soapLevel);
        }
        // 세정제 전진 정지
        if (!_Y0C1 && _prevY0C1)
        {
            soapParticle?.Stop();
        }

        // 세정제 후진 시작
        if (_Y0C2 && !_prevY0C2)
        {
            soapCylinderAnimator?.SetTrigger("SoapBackward");
        }

        // 물 변화
        if (_Y0C0 && !_prevY0C0)
        {
            waterParticle?.gameObject.SetActive(true);
            waterParticle?.Play();
            SoapUsageLogger.Instance?.LogWater();
        }
        if (!_Y0C0 && _prevY0C0)
        {
            waterParticle?.Stop();
        }

        // 바람 변화
        if (_Y0C3 && !_prevY0C3)
        {
            airParticle?.gameObject.SetActive(true);
            airParticle?.Play();
            SoapUsageLogger.Instance?.LogAir();
        }
        if (!_Y0C3 && _prevY0C3)
        {
            airParticle?.Stop();
        }

        // StationData 갱신
        stationData.isSoapRunning     = soapActive;
        stationData.isWaterRunning    = _Y0C0;
        stationData.isAirRunning      = _Y0C3;
        stationData.isSoapForward     = _Y0C1;
        stationData.isSoapBackward    = _Y0C2;
        stationData.isManualWaterMode = _M10;
        stationData.isManualSoapMode  = _M20;
        stationData.isManualAirMode   = _M30;
        stationData.isAutoMode        = _M40;
        stationData.currentStep       = ComputeStep();

        // 이벤트 발행 (변화 시)
        if (soapActive != _prevSoapActive)
        {
            OnSoapUpdated?.Invoke();
        }
        if (_Y0C0 != _prevY0C0)
        {
            OnWaterUpdated?.Invoke();
        }
        if (_Y0C3 != _prevY0C3)
        {
            OnAirUpdated?.Invoke();
        }

        _prevY0C0       = _Y0C0;
        _prevY0C1       = _Y0C1;
        _prevY0C2       = _Y0C2;
        _prevY0C3       = _Y0C3;
        _prevSoapActive = soapActive;
    }

    private int ComputeStep()
    {
        return StationData.ComputeStep(_M10 || _M20 || _M30 || _M40, _Y0C1, _Y0C2, _Y0C0, _Y0C3);
    }

    // ════════════════════════════════════════════════════════════════
    //  PLC 모드 — stationData 미러링
    // ════════════════════════════════════════════════════════════════

    private void MirrorPLCState()
    {
        bool soap  = stationData.isSoapRunning;
        bool water = stationData.isWaterRunning;
        bool air   = stationData.isAirRunning;
        bool fwd   = stationData.isSoapForward;
        bool bwd   = stationData.isSoapBackward;

        if (soap != _mirrorSoap)
        {
            _mirrorSoap = soap;
            if (soap)
            {
                soapParticle?.gameObject.SetActive(true);
                soapParticle?.Play();
            }
            else
            {
                soapParticle?.Stop();
            }
            OnSoapUpdated?.Invoke();
        }
        if (water != _mirrorWater)
        {
            _mirrorWater = water;
            if (water)
            {
                waterParticle?.gameObject.SetActive(true);
                waterParticle?.Play();
            }
            else
            {
                waterParticle?.Stop();
            }
            OnWaterUpdated?.Invoke();
        }
        if (air != _mirrorAir)
        {
            _mirrorAir = air;
            if (air)
            {
                airParticle?.gameObject.SetActive(true);
                airParticle?.Play();
            }
            else
            {
                airParticle?.Stop();
            }
            OnAirUpdated?.Invoke();
        }

        // 실린더 애니메이션 (상승엣지 기반)
        if (fwd && !_mirrorFwd)
        {
            soapCylinderAnimator?.SetTrigger("SoapForward");
        }
        if (bwd && !_mirrorBwd)
        {
            soapCylinderAnimator?.SetTrigger("SoapBackward");
        }
        _mirrorFwd = fwd;
        _mirrorBwd = bwd;
    }

    // ════════════════════════════════════════════════════════════════
    //  HMI 요청 메서드 (버튼 클릭)
    // ════════════════════════════════════════════════════════════════

    /// <summary>세정제 버튼 → X0A8 상승엣지 (수동 세정제 모드)</summary>
    public void RequestSoap()
    {
        if (!CanAcceptRequest()) return;
        if (AppModeManager.IsTestMode)
        {
            _X0A8 = true;
        }
        else
        {
            StartCoroutine(ButtonPulse(
                () => NetworkManager.Instance?.WriteSoapButton(true),
                () => NetworkManager.Instance?.WriteSoapButton(false)));
        }
    }

    /// <summary>물 버튼 → X0A7 상승엣지 (수동 물 모드)</summary>
    public void RequestWater()
    {
        if (!CanAcceptRequest()) return;
        if (AppModeManager.IsTestMode)
        {
            _X0A7 = true;
        }
        else
        {
            StartCoroutine(ButtonPulse(
                () => NetworkManager.Instance?.WriteWaterButton(true),
                () => NetworkManager.Instance?.WriteWaterButton(false)));
        }
    }

    /// <summary>바람 버튼 → X0A9 상승엣지 (수동 바람 모드)</summary>
    public void RequestAir()
    {
        if (!CanAcceptRequest()) return;
        if (AppModeManager.IsTestMode)
        {
            _X0A9 = true;
        }
        else
        {
            StartCoroutine(ButtonPulse(
                () => NetworkManager.Instance?.WriteAirButton(true),
                () => NetworkManager.Instance?.WriteAirButton(false)));
        }
    }

    /// <summary>손 센서 시뮬레이션 → X0A6 상승엣지 (자동 모드)</summary>
    [ContextMenu("TEST: 손 센서 → 자동 모드 시작")]
    public void TriggerHandSensor()
    {
        if (!CanAcceptRequest()) return;
        if (AppModeManager.IsTestMode)
        {
            _X0A6 = true;
        }
        else
        {
            StartCoroutine(ButtonPulse(
                () => NetworkManager.Instance?.WriteHandSensor(true),
                () => NetworkManager.Instance?.WriteHandSensor(false)));
        }
    }

    private bool CanAcceptRequest()
    {
        if (FloorManager.Instance != null && !FloorManager.Instance.IsRealPLCFloor)
        {
            return false;
        }
        if (AppModeManager.IsTestMode)
        {
            return true;   // 래더 인터록이 직접 처리
        }
        return !(stationData.isManualWaterMode || stationData.isManualSoapMode ||
                 stationData.isManualAirMode   || stationData.isAutoMode);
    }

    // ════════════════════════════════════════════════════════════════
    //  헬퍼
    // ════════════════════════════════════════════════════════════════

    /// <summary>OUT T 방식 타이머 — §규칙 2: 조건 OFF 시 즉시 리셋</summary>
    private void UpdateTimer(bool cond, ref float acc, ref bool contact, float setTime)
    {
        if (cond)
        {
            acc += Time.deltaTime;
            if (acc >= setTime)
            {
                contact = true;
            }
        }
        else
        {
            acc = 0f;
            contact = false;
        }
    }

    private bool Rise(bool cur, bool prev)
    {
        return cur && !prev;
    }

    private bool Fall(bool cur, bool prev)
    {
        return !cur && prev;
    }

    private IEnumerator ButtonPulse(Action writeTrue, Action writeFalse)
    {
        writeTrue();
        yield return new WaitForSeconds(0.15f);
        writeFalse();
    }

    /// <summary>층 전환 시 모든 동작 강제 중단</summary>
    private void ForceStopAll()
    {
        // 래더 상태 전체 리셋
        _M10 = false;
        _M20 = false;
        _M30 = false;
        _M40 = false;
        _M0  = false;
        _M1  = false;
        _M2  = false;
        _M3  = false;
        _M4  = false;
        _M5  = false;
        _M6  = false;
        _M7  = false;
        _T0  = false;
        _T1  = false;
        _T2  = false;
        _T3  = false;
        _T4  = false;
        _t0  = 0f;
        _t1  = 0f;
        _t2  = 0f;
        _t3  = 0f;
        _t4  = 0f;
        _C0val = 0;
        _C0 = false;
        _Y0C0 = false;
        _Y0C1 = false;
        _Y0C2 = false;
        _Y0C3 = false;
        _X0A0 = true;
        _X0A1 = false;   // 실린더 초기 위치 복원
        _fwdTimer = -1f;
        _bwdTimer = -1f;

        bool wasActive = stationData.isSoapRunning || stationData.isWaterRunning || stationData.isAirRunning;

        stationData.isSoapRunning     = false;
        stationData.isWaterRunning    = false;
        stationData.isAirRunning      = false;
        stationData.isSoapForward     = false;
        stationData.isSoapBackward    = false;
        stationData.isManualWaterMode = false;
        stationData.isManualSoapMode  = false;
        stationData.isManualAirMode   = false;
        stationData.isAutoMode        = false;
        stationData.currentStep       = 0;

        soapParticle?.Stop();
        waterParticle?.Stop();
        airParticle?.Stop();

        _mirrorSoap     = false;
        _mirrorWater    = false;
        _mirrorAir      = false;
        _mirrorFwd      = false;
        _mirrorBwd      = false;
        _prevY0C0       = false;
        _prevY0C1       = false;
        _prevY0C2       = false;
        _prevY0C3       = false;
        _prevSoapActive = false;

        if (wasActive)
        {
            OnSoapUpdated?.Invoke();
            OnWaterUpdated?.Invoke();
            OnAirUpdated?.Invoke();
        }
    }
}
