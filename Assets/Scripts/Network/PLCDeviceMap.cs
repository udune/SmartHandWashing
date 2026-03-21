[System.Serializable]
public class PLCDeviceMap
{
    public string soapLevel  = "D0";
    public string soapBtn    = "M0";
    public string waterBtn   = "M1";
    public string airBtn     = "M2";
    public string soapAlarm  = "M10";
    public string waterAlarm = "M11";
    public string usageCount = "D10";
}

[System.Serializable]
public class PLCConfig
{
    public bool   useMock        = true;
    public string ip             = "192.168.1.10";
    public int    port           = 5007;
    public int    pollIntervalMs = 100;
    public int    timeoutMs      = 2000;
    public PLCDeviceMap devices  = new PLCDeviceMap();
}
