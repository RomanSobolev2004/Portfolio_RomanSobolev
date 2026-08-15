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

public class UnitInstaller : IUnitInstaller
{
    private DiContainer _container;
    private IUnitAngar _unitAngar;
    private IGameGrid _grid;
    private INPCSystem _npcSystem;
    private IUnitMovementSystem _movement;
    private ICellSystem _cellSystem;
    private IUnitTeams _teams;

    [Inject]
    private void Construct(DiContainer container, IGameGrid grid, ICellSystem cellSystem, INPCSystem npcSystem, IUnitAngar angar, IUnitTeams teams, IUnitMovementSystem movement)
    {
        _grid = grid;
        _container = container;
        _movement = movement;
        _npcSystem = npcSystem;
        _teams = teams;
        _unitAngar = angar;
        _cellSystem = cellSystem;
    }

    public IGameIdle SpawnIdle(IdleType type, string classification, Vector2 position)
    {
        var prefab = _unitAngar.GetPrefab<IGameIdle>(type, classification);

        IGameIdle idle = _container.InstantiatePrefabForComponent<IGameIdle>(prefab as MonoBehaviour, position, Quaternion.identity, null);

        idle.SetIdleData();

        return idle;
    }

    public IGameUnit InstallUnit(IGameIdle spawnedIdle, Vector2Int cellPosition, float rotation, int team, bool isNPC)
    {
        IGameUnit unit = InstallIdle(spawnedIdle, cellPosition, rotation) as IGameUnit;

        if (isNPC) _npcSystem.CreateNPC(unit);

        _teams.AddOrCreate(team, unit, isNPC ? FSM.Enums.Player_EnemyEnum.Enemy : FSM.Enums.Player_EnemyEnum.Player);

        return unit;
    }
    public IGameIdle InstallIdle(IGameIdle spawnedIdle, Vector2Int cellPosition, float rotation)
    {
        _movement.Rotation.RotateTransform(spawnedIdle as IGameUnit, rotation, out Transform unitTransform);
        unitTransform.position = _cellSystem.CellInPosition(cellPosition);

        _grid.AddIdleToGrid(spawnedIdle, cellPosition);

        return spawnedIdle;
    }
}