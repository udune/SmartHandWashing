using System;

/// <summary>
/// 층별 데이터 모델.
/// JSON 역직렬화를 위해 [Serializable] 속성 필수.
/// </summary>
[Serializable]
public class FloorData
{
    public string floorId;
    public string floorName;
    public float  soapLevel;
    public int[]  hourlyUsage;
    public bool   isRealPLC;
}

/// <summary>
/// FloorData 배열을 감싸는 래퍼 클래스.
/// JsonUtility가 최상위 배열을 직접 파싱하지 못하므로 객체로 감싸야 함.
/// </summary>
[Serializable]
public class FloorDataList
{
    public FloorData[] floors;
}
