public class NPC : INPC
{
    private NPC_Data _data;

    private NPC_Mind_Tactic _tactic;
    private NPC_Mind_Movement _movement;
    private NPC_Mind_Attacking _attacking;
    private NPC_Mind_Abilities _abilities;

    private ILevelDifficult _difficult;

    public NPC(IUnitTeams teams, IEffectManager effects, IGameGrid grid, ICellSystem cellSystem, ILevelDifficult levelDifficult)
    {
        _difficult = levelDifficult;

        SetPPData();

        _tactic = new NPC_Mind_Tactic(cellSystem, _difficult, effects, teams, _data, grid);
        _movement = new NPC_Mind_Movement(cellSystem, _difficult, _data, grid);
        _attacking = new NPC_Mind_Attacking(effects, _difficult, _data, grid);
        _abilities = new NPC_Mind_Abilities(effects, _data, grid, _difficult);
    }

    public IGameUnit Unit
    {
        get => _data.Unit;
        set => _data.Unit = value;
    }
    public IGameUnit Target
    {
        get => _data.Target;
        set => _data.Target = value;
    }
    public Vector2Int TargetPos => _data.TargetPos;
    public Vector2Int UnitPos => _data.UnitPos;
    public NPC_Mind_Tactic Tactic => _tactic;
    public NPC_Mind_Movement Movement => _movement;
    public NPC_Mind_Attacking Attacking => _attacking;
    public NPC_Mind_Abilities Abilities => _abilities;

    private void SetPPData()
    {
        _data = new NPC_Data();

        _data.Health_PP = _difficult.GetCurrentLevelDifficult().GetValue<float>("NPC_PP_HP");
        _data.Attack_PP = _difficult.GetCurrentLevelDifficult().GetValue<float>("NPC_PP_DMG");
        _data.Distance_PP = _difficult.GetCurrentLevelDifficult().GetValue<float>("NPC_PP_DIS");
        _data.OptimalAttackDis = _difficult.GetCurrentLevelDifficult().GetValue<int>("OPT_ATK");
        _data.AttackSpread = _difficult.GetCurrentLevelDifficult().GetValue<int>("ATK_SPRD");
    }
}

public class NPC_core
{
    protected NPC_Data _data;
    protected IGameGrid _gameGrid;

    public NPC_core(NPC_Data data, IGameGrid grid)
    {
        _data = data;
        _gameGrid = grid;
    }

    protected int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
    protected Vector2Int RayObstacleCheck(Vector2Int start, Vector2Int end)
    {
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);

        int sx = start.x < end.x ? 1 : -1;
        int sy = start.y < end.y ? 1 : -1;
        int err = dx - dy;

        Vector2Int current = start;
        int gridLayer = _gameGrid.FindGridLayerFromIdle(_data.Unit);

        while (true)
        {
            if (current == end) break;

            int e2 = err * 2;

            if (e2 > -dy)
            {
                err -= dy;
                current.x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                current.y += sy;
            }

            if (_gameGrid.GetGrid(gridLayer).ContainsKey(current))
            {
                IGameIdle idle = _gameGrid.GetGrid(gridLayer)[current];

                IdleType type = idle.Info.GetValue<IdleType>("TYPE");
                if (type == IdleType.Obstacle)
                {
                    return current;
                }
            }
        }
        return end;
    }
    protected bool DoAfterFalliability(float falliability)
    {
        float randomValue = Random.Range(0, 100);

        if (randomValue >= falliability)
        {
            return false;
        }

        return true;
    }
}