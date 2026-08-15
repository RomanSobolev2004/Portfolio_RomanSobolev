public class UnitAngar : MonoBehaviour, IUnitAngar
{
    [SerializeField] private AngarSO angarSO;

    private Dictionary<IdleType, Dictionary<string, IGameIdle>> _prefabAngar;

    public PrefabType GetPrefab<PrefabType>(IdleType type, string classification) where PrefabType : IGameIdle
    {
        return (PrefabType)_prefabAngar[type][classification];
    }

    public void SetPrefabAngar()
    {
        _prefabAngar = new Dictionary<IdleType, Dictionary<string, IGameIdle>>();

        _prefabAngar.Add(IdleType.Tank, TanksPrefabDictionary(angarSO));
    }

    private Dictionary<string, IGameIdle> TanksPrefabDictionary(AngarSO angarSO)
    {
        Dictionary<string, IGameIdle> tankDic = new Dictionary<string, IGameIdle>();

        //(Tier, Class) = Key
        for (int t = 0; t < angarSO.TanksTierList.Count; t++)
        {
            for (int c = 0; c < angarSO.TanksTierList[t].TanksClassList.Count; c++)
            {
                string classification = $"{t + 1},{c + 1}";

                IGameIdle idle = angarSO.TanksTierList[t].TanksClassList[c];
                tankDic.Add(classification, idle);
                idle.SetIdleData();
            }
        }

        return tankDic;
    }
}