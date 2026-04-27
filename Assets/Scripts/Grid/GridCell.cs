using UnityEngine;

/// <summary>
/// 棋盘格子
/// </summary>
public class GridCell
{
    public Vector2Int Position { get; private set; }
    public bool IsOccupied => Occupant != null;
    public Hero Occupant { get; private set; }

    public int X => Position.x;
    public int Y => Position.y;

    public GridCell(int x, int y)
    {
        Position = new Vector2Int(x, y);
    }

    public void PlaceHero(Hero hero)
    {
        Occupant = hero;
        if (hero != null)
        {
            hero.GridPosition = Position;
        }
    }

    public void RemoveHero()
    {
        Occupant = null;
    }

    public override string ToString()
    {
        return $"[{X},{Y}] {(IsOccupied ? Occupant.Data.heroName : "空")}";
    }
}
