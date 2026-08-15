public class CellSystem : MonoBehaviour, ICellSystem
{
    [SerializeField] private Transform nullCell;
    [SerializeField] private CellGrid cellLimits;

    private static Vector2 CELL_OFFSET = new Vector2(0.26f, 0.2573f);

    public static Vector2 Offset { get { return CELL_OFFSET; } }
    public CellGrid CellsLimits { get { return cellLimits; } }
    public bool IsCellAvaliable(Vector2Int cell)
    {
        if (cell.x < cellLimits.LeftDown_Corner.x || cell.x >= cellLimits.RightUp_Corner.x) return false;
        if (cell.y < cellLimits.LeftDown_Corner.y || cell.y >= cellLimits.RightUp_Corner.y) return false;
        return true;
    }
    public Vector2Int PositionInCell(Vector2 pos)
    {
        pos -= (Vector2)nullCell.position;
        int x = (int)Mathf.Round(pos.x / CELL_OFFSET.x);
        int y = (int)Mathf.Round(pos.y / CELL_OFFSET.y);

        return new Vector2Int(x, y);
    }
    public Vector2 CellInPosition(Vector2Int cellPos)
    {
        float x = (cellPos.x * CELL_OFFSET.x) + nullCell.position.x;
        float y = (cellPos.y * CELL_OFFSET.y) + nullCell.position.y;

        return new Vector2(x, y);
    }
    public Vector2 ToNearCellPosition(Vector2 postition)
    {
        Vector2Int posCell = PositionInCell(postition);
        postition = CellInPosition(posCell);

        return postition;
    }
}

public class GameGrid : IGameGrid
{
    private List<Dictionary<Vector2Int, IGameIdle>> _gameGridLayers;

    private ICellSystem _iCellSystem;

    [Inject]
    private void Construct(ICellSystem ICellSystem)
    {
        _iCellSystem = ICellSystem;

        _gameGridLayers = new List<Dictionary<Vector2Int, IGameIdle>>();
        _gameGridLayers.Add(new Dictionary<Vector2Int, IGameIdle>()); // Default grid (index - 0)
    }

    public IReadOnlyDictionary<Vector2Int, IGameIdle> GetGrid(int layer = 0) => _gameGridLayers[layer];
    public IReadOnlyList<IReadOnlyDictionary<Vector2Int, IGameIdle>> GetGridLayers() => _gameGridLayers;

    public void RemoveIdleFromGridCell(Vector2Int cell, int layer = 0)
    {
        _gameGridLayers[layer].Remove(cell);
    }

    public void AddIdleToGrid(IGameIdle idle, int layer = 0)
    {
        Vector2Int unitPosInCell = _iCellSystem.PositionInCell((idle as MonoBehaviour).transform.position);
        AddIdleToGrid(idle, unitPosInCell, layer);
    }
    public void AddIdleToGrid(IGameIdle idle, Vector2Int spawnCell, int layer = 0)
    {
        FindAndCreateLayerGrid(layer);

        foreach (Vector2Int dim in idle.Info.GetValue<IReadOnlyList<Vector2Int>>("DIM"))
        {
            Vector2Int pos = spawnCell + dim;
            _gameGridLayers[layer].Add(pos, idle);
        }
    }

    public void ClearIdleFromGrid(IGameIdle idle, int layer = 0)
    {
        Vector2Int unitPosInCell = _iCellSystem.PositionInCell((idle as MonoBehaviour).transform.position);

        foreach (Vector2Int dim in idle.Info.GetValue<IReadOnlyList<Vector2Int>>("DIM"))
        {
            Vector2Int pos = unitPosInCell + dim;
            RemoveIdleFromGridCell(pos, layer);
        }
    }

    public IGameIdle FindIdleOnGrid(Vector2Int cell, int layer = 0)
    {
        return TryToFindIdleOnGrid(cell, layer) ? GetGrid(layer)[cell] : null;
    }
    public bool TryToFindIdleOnGrid(Vector2Int cell, int layer = 0)
    {
        return TryFindLayer(layer) && _gameGridLayers[layer].ContainsKey(cell);
    }
    public bool TryToFindIdleOnGridSelfExcludive(IGameIdle self, Vector2Int cell, int layer = 0)
    {
        return TryToFindIdleOnGrid(cell, layer)
            && FindIdleOnGrid(cell) != self;
    }
    public int FindGridLayerFromIdle(IGameIdle idle)
    {
        Vector2Int cell = _iCellSystem.PositionInCell((idle as MonoBehaviour).transform.position) + idle.Info.GetValue<IReadOnlyList<Vector2Int>>("DIM")[0];
        for (int i = 0; i < _gameGridLayers.Count; i++)
        {
            if (FindIdleOnGrid(cell, i) == idle) return i;
        }

        return -1;
    }
    public bool TryFindLayer(int layer)
    {
        if (layer >= 0 && layer < _gameGridLayers.Count) return true;

        return false;
    }

    public bool IdleIsUnder(Vector2Int idleCell, bool full = false)
    {
        return HasIdleOnNearLayer(idleCell, 1, full);
    }
    public bool IdleIsOver(Vector2Int idleCell, bool full = false)
    {
        return HasIdleOnNearLayer(idleCell, -1, full);
    }

    public void ChekForDiedIdle(Vector2Int cell, int layer = 0)
    {
        IGameIdle idle = FindIdleOnGrid(cell, layer);
        if (idle == null) return;

        if (idle.Info.GetValue<int>("HP") <= 0)
        {
            ClearIdleFromGrid(idle);
        }
    }
    public void CheckGridForDiedIdles(int layer = 0)
    {
        foreach (Vector2Int pos in GetGrid(layer).Keys.ToList())
        {
            ChekForDiedIdle(pos, layer);
        }
    }
    public void CheckForIncorrenctDimention(Vector2Int cell, int layer = 0)
    {
        IGameIdle idle = FindIdleOnGrid(cell, layer);

        if (idle is null) return;

        Vector2Int idleCell = _iCellSystem.PositionInCell((idle as MonoBehaviour).transform.position);

        bool hasCell = false;
        foreach (Vector2Int dim in idle.Info.GetValue<IReadOnlyList<Vector2Int>>("DIM"))
        {
            if (cell == dim + idleCell) hasCell = true;
        }

        if (!hasCell) RemoveIdleFromGridCell(cell, layer);
    }

    public Vector2Int IdleCellInDimention(Vector2Int cell, int layer = 0)
    {
        IGameIdle idle = _gameGridLayers[layer][cell];

        Vector2Int idleCell = _iCellSystem.PositionInCell((idle as MonoBehaviour).transform.position);

        foreach (Vector2Int dim in idle.Info.GetValue<IReadOnlyList<Vector2Int>>("DIM"))
        {
            if (cell == dim + idleCell) return dim;
        }

        return Vector2Int.zero;
    }

    private void FindAndCreateLayerGrid(int index)
    {
        if (_gameGridLayers.Count <= index)
        {
            int count = index - _gameGridLayers.Count + 1;

            for (int i = 0; i < count; i++)
            {
                _gameGridLayers.Add(new Dictionary<Vector2Int, IGameIdle>());
            }
        }
    }
    private bool HasIdleOnNearLayer(Vector2Int idleCell, int layerOffcet, bool full)
    {
        IGameIdle idle = FindIdleOnGrid(idleCell);
        if (idle is null) return false;

        int layerOver = FindGridLayerFromIdle(idle) + layerOffcet;
        if (TryFindLayer(layerOver))
        {
            foreach (Vector2Int dim in idle.Info.GetValue<IReadOnlyList<Vector2Int>>("DIM"))
            {
                if (!TryToFindIdleOnGrid(dim + idleCell, layerOver))
                {
                    if (full) return false;
                }
                else
                {
                    if (!full) return true;
                }

            }
        }

        return true;
    }
}