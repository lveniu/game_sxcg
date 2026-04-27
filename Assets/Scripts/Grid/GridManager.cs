using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 棋盘管理器 — 3×4 棋盘，支持放置/移除/寻敌
/// </summary>
public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("棋盘大小")]
    public int width = 3;
    public int height = 4;

    private GridCell[,] cells;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeGrid();
    }

    /// <summary>
    /// 初始化棋盘
    /// </summary>
    public void InitializeGrid()
    {
        cells = new GridCell[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y] = new GridCell(x, y);
            }
        }
    }

    /// <summary>
    /// 获取指定位置的格子
    /// </summary>
    public GridCell GetCell(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return null;
        return cells[x, y];
    }

    public GridCell GetCell(Vector2Int pos) => GetCell(pos.x, pos.y);

    /// <summary>
    /// 放置英雄到指定位置
    /// </summary>
    public bool PlaceHero(Hero hero, int x, int y)
    {
        var cell = GetCell(x, y);
        if (cell == null || cell.IsOccupied) return false;

        // 从原位置移除
        if (hero.GridPosition.x >= 0 && hero.GridPosition.y >= 0)
        {
            var oldCell = GetCell(hero.GridPosition);
            oldCell?.RemoveHero();
        }

        cell.PlaceHero(hero);
        return true;
    }

    /// <summary>
    /// 移除指定位置的英雄
    /// </summary>
    public bool RemoveHero(int x, int y)
    {
        var cell = GetCell(x, y);
        if (cell == null || !cell.IsOccupied) return false;
        cell.RemoveHero();
        return true;
    }

    /// <summary>
    /// 获取某一行的所有格子
    /// </summary>
    public List<GridCell> GetRowCells(int y)
    {
        var result = new List<GridCell>();
        for (int x = 0; x < width; x++)
        {
            result.Add(cells[x, y]);
        }
        return result;
    }

    /// <summary>
    /// 获取某一列的所有格子
    /// </summary>
    public List<GridCell> GetColumnCells(int x)
    {
        var result = new List<GridCell>();
        for (int y = 0; y < height; y++)
        {
            result.Add(cells[x, y]);
        }
        return result;
    }

    /// <summary>
    /// 获取指定位置所属的排（前/中/后）
    /// </summary>
    public GridRow GetRow(Vector2Int position)
    {
        // 假设棋盘分为三区域：y=0-1前排，y=2-3中排，y=4+后排
        // MVP中 3×4 棋盘，y=0,1 为前排，y=2 为中排，y=3 为后排
        if (height >= 4)
        {
            if (position.y <= 1) return GridRow.Front;
            if (position.y == 2) return GridRow.Middle;
            return GridRow.Back;
        }
        return GridRow.Middle;
    }

    /// <summary>
    /// 获取场上所有英雄
    /// </summary>
    public List<Hero> GetAllHeroes()
    {
        var result = new List<Hero>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (cells[x, y].IsOccupied)
                    result.Add(cells[x, y].Occupant);
            }
        }
        return result;
    }

    /// <summary>
    /// 获取指定排的英雄
    /// </summary>
    public List<Hero> GetHeroesByRow(GridRow row)
    {
        var result = new List<Hero>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = cells[x, y];
                if (cell.IsOccupied && GetRow(cell.Position) == row)
                    result.Add(cell.Occupant);
            }
        }
        return result;
    }

    /// <summary>
    /// 寻找距离最近的敌人
    /// </summary>
    public Hero FindNearestEnemy(Hero self, List<Hero> enemies)
    {
        if (enemies == null || enemies.Count == 0) return null;

        Hero nearest = null;
        float minDist = float.MaxValue;

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            float dist = Vector2Int.Distance(self.GridPosition, enemy.GridPosition);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    /// <summary>
    /// 清空棋盘
    /// </summary>
    public void ClearGrid()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cells[x, y].RemoveHero();
            }
        }
    }

    /// <summary>
    /// 打印棋盘状态（调试）
    /// </summary>
    public void PrintGrid()
    {
        Debug.Log("=== 棋盘状态 ===");
        for (int y = height - 1; y >= 0; y--)
        {
            string row = $"行{y}: ";
            for (int x = 0; x < width; x++)
            {
                row += cells[x, y].IsOccupied ? $"[{cells[x, y].Occupant.Data.heroName}]" : "[ ]";
            }
            Debug.Log(row);
        }
    }
}
