using UnityEngine;
using System;
using System.Collections;

public class StationController : MonoBehaviour
{
    [Header("Data")]
    public StationData stationData;

    [Header("Particles")]
    public ParticleSystem soapParticle;
    public ParticleSystem waterParticle;
    public ParticleSystem airParticle;

    public event Action OnSoapUpdated;
    public event Action OnWaterUpdated;
    public event Action OnAirUpdated;

    private Coroutine _soapCoroutine;
    private Coroutine _waterCoroutine;
    private Coroutine _airCoroutine;

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

    public void ActivateSoap()
    {
        // 인터락: 다른 동작 중이면 실행 불가
        if (IsAnyRunning() || stationData.soapLevel <= 0f)
        {
            return;
        }

        if (_soapCoroutine != null)
        {
            StopCoroutine(_soapCoroutine);
        }

        _soapCoroutine = StartCoroutine(RunDispenser(
            setter: v => stationData.isSoapRunning = v,
            duration: 3f,
            particle: soapParticle,
            onStart: () =>
            {
                stationData.UseSoap();
                OnSoapUpdated?.Invoke();
            },
            onEnd:   () => OnSoapUpdated?.Invoke()
        ));
    }

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

    private IEnumerator RunDispenser(
        Action<bool> setter, float duration,
        ParticleSystem particle,
        Action onStart, Action onEnd)
    {
        setter(true);
        if (particle != null)
        {
            particle.gameObject.SetActive(true); 
            particle.Play();
        }
        onStart?.Invoke();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        setter(false);
        if (particle != null)
        {
            particle.Stop();
        }
        onEnd?.Invoke();
    }

    // 타이머 남은 시간 조회용 (UI에서 폴링)
    public float GetSoapRemaining()
    {
        return GetRemaining(_soapCoroutine, 3f);
    }

    public float GetWaterRemaining()
    {
        return GetRemaining(_waterCoroutine, 10f);
    }

    public float GetAirRemaining()
    {
        return GetRemaining(_airCoroutine, 10f);
    }

    private float GetRemaining(Coroutine c, float total)
    {
        if (c != null)
        {
            return total;
        }
        return 0f;
    }
}
