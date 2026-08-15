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


    ...


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
   

    ...


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

    
    ...


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