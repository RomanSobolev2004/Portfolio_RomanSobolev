public static class SaveLoadManger
{
    public static void SaveData(SaveData data, string key = null)
    {
        if (key == null) key = data.GetType().Name;

        ES3.Save(key, data);
    }
    public static DataType LoadData<DataType>(string key = null) where DataType : SaveData
    {
        if (key == null) key = typeof(DataType).Name;

        if (ES3.KeyExists(key)) return ES3.Load<SaveData>(key) as DataType;

        return null;
    }

    public static void DeleteData(string key)
    {
        ES3.DeleteKey(key);
    }
}

public class SaveData { }

public class GameSaveData : SaveData
{
    [ES3Serializable] public int SceneIndex { get; set; } = -1;
    [ES3Serializable] public Dictionary<int, bool> FirstLevelEnters { get; set; } = new();

    [ES3Serializable] public string SaveDate { get; set; }
    [ES3Serializable] public List<byte> SaveScreenshotTextureInBytes { get; set; } = new();

    [ES3Serializable] public Dictionary<string, ExpData> DataModels { get; set; } = new();

    public bool IsLoadedLevelLauchedFirst() => FirstLevelEnters[SceneIndex];
}

public class ExpData
{
    public ExpData() { }
    public ExpData(WrappedParameters defaultParameters)
    {
        this.defaultParameters = defaultParameters;
    }

    [ES3Serializable] public WrappedParameters defaultParameters;
}

public interface ISaveable
{
    bool EnableSave { get; }

    string SaveID { get; }

    ExpData PackData();
    void LoadData(ExpData data);
}

public class GameSaveLoad
{
    private string m_saveKey;
    private string m_slotKey;

    private GameSaveData m_data;
    private int m_dataSlot;

    private HashSet<ISaveable> m_registred = new();

    public GameSaveLoad(string saveKey, string slotKey)
    {
        m_saveKey = saveKey;
        m_slotKey = slotKey;

        SaveLoadRegistry.BindInstance(this);
    }

    public void NewSave(int slotID, int defaultSceneNum)
    {
        m_data = new GameSaveData();
        m_data.SceneIndex = defaultSceneNum;
        m_dataSlot = slotID;
        m_registred.Clear();
        SaveCurrentDataToSlot(m_dataSlot);
    }
    public void SaveCurrentDataToSlot(int slotID, Texture2D cameraScreenshot = null)
    {
        m_data.SaveDate = DateTime.Now.ToString("yyyy-MM-dd");
        if (cameraScreenshot) m_data.SaveScreenshotTextureInBytes = cameraScreenshot.EncodeToPNG().ToList();

        SaveRegistred();
        SaveLoadManger.SaveData(m_data, GenerateSaveKey(slotID));
    }
    public void ResetData(int slotID)
    {
        SaveLoadManger.DeleteData(GenerateSaveKey(slotID));
    }

    public void LoadDataFromSlotToCache(int slotID)
    {
        m_registred.Clear();
        m_dataSlot = slotID;
        m_data = ReadDataFromSlot(m_dataSlot);
    }
    public GameSaveData ReadDataFromSlot(int slotID)
    {
        return SaveLoadManger.LoadData<GameSaveData>(GenerateSaveKey(slotID));
    }

    public GameSaveData GetLoadedData() => m_data;
    public bool TryGetLoadedData(out GameSaveData data)
    {
        data = GetLoadedData();
        return data != null;
    }

    public void AutoSave(Texture2D cameraScreenshot = null)
    {
        SaveCurrentDataToSlot(m_dataSlot, cameraScreenshot);
    }


    public void Register(ISaveable obj)
    {
        m_registred.Add(obj);
    }
    public void Unregister(ISaveable obj)
    {
        m_registred.Remove(obj);
    }

    public SaveableType GetRegistred<SaveableType>(string saveID) where SaveableType : ISaveable
    {
        var finded = m_registred.FirstOrDefault(obj => obj.SaveID == saveID);

        return (SaveableType)finded;
    }

    public void SaveRegistred()
    {
        var data = GetLoadedData();

        foreach (var registred in m_registred)
        {
            var id = registred.SaveID;

            var packed = registred.PackData();
            if (registred.EnableSave && packed != null) data.DataModels[id] = packed;
        }
    }
    public void LoadRegistred()
    {
        var data = GetLoadedData();

        foreach (var registred in m_registred)
        {
            if (registred.EnableSave && data.DataModels.TryGetValue(registred.SaveID, out var saved))
            {
                registred.LoadData(saved);
            }
        }
    }

    private string GenerateSaveKey(int slotID)
    {
        return $"{m_slotKey}-{slotID}-{m_saveKey}";
    }
}

public static class SaveLoadRegistry
{
    private static GameSaveLoad m_instace;

    public static void BindInstance(GameSaveLoad saveLoad)
    {
        if (m_instace == null) m_instace = saveLoad;
    }

    public static void Register(ISaveable obj)
    {
        m_instace.Register(obj);
    }
    public static void Unregister(ISaveable obj)
    {
        m_instace.Unregister(obj);
    }
    public static SaveableType GetRegistred<SaveableType>(string saveID) where SaveableType : ISaveable
    {
        return m_instace.GetRegistred<SaveableType>(saveID);
    }

}