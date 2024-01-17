using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileBoard : MonoBehaviour
{
    public GameManager gameManager;
    public Tile tilePrefab;
    public TileState[] tileStates;

    private TileGrid grid;
    private List<Tile> tiles;
    private bool waiting;

    private Vector2 startTouchPosition;
    private Vector2 endTouchPosition;
    private bool isSwiping = false;

    private void Awake()
    {
        grid = GetComponentInChildren<TileGrid>();
        tiles = new List<Tile>(16);
    }

    public void ClearBoard()
    {
        foreach (var cell in grid.cells) {
            cell.tile = null;
        }

        foreach (var tile in tiles) {
            Destroy(tile.gameObject);
        }

        tiles.Clear();
    }

    public void CreateTile()
    {
        Tile tile = Instantiate(tilePrefab, grid.transform);
        tile.SetState(tileStates[0]);
        tile.Spawn(grid.GetRandomEmptyCell());
        tiles.Add(tile);
    }

    private void Update()
    {
        if (!waiting)
        {

            ControlledBySwipe();
            ControlledByKeyboard();
        }
    }

    private void ControlledByKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            Move(Vector2Int.up, 0, 1, 1, 1);
        }
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Move(Vector2Int.left, 1, 1, 0, 1);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            Move(Vector2Int.down, 0, 1, grid.height - 2, -1);
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            Move(Vector2Int.right, grid.width - 2, -1, 0, 1);
        }
    }

    private void ControlledBySwipe()
    {
        Debug.Log($"Input.touchCount: {Input.touchCount}");
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    startTouchPosition = touch.position;
                    isSwiping = true;
                    break;

                case TouchPhase.Moved:
                    endTouchPosition = touch.position;
                    break;

                case TouchPhase.Ended:
                    if (isSwiping)
                    {
                        Vector2 swipeDirection = endTouchPosition - startTouchPosition;

                        if (swipeDirection.magnitude >= 50) // Điều này để xác định liệu swipe có đủ lớn hay không.
                        {
                            if (Mathf.Abs(swipeDirection.x) > Mathf.Abs(swipeDirection.y))
                            {
                                // Swipe ngang (trái hoặc phải)
                                if (swipeDirection.x > 0)
                                {
                                    Debug.Log("Swipe phải");
                                    Move(Vector2Int.right, grid.width - 2, -1, 0, 1);
                                }
                                else
                                {
                                    Debug.Log("Swipe trái");
                                    Move(Vector2Int.left, 1, 1, 0, 1);
                                }
                            }
                            else
                            {
                                // Swipe dọc (lên hoặc xuống)
                                if (swipeDirection.y > 0)
                                {
                                    Debug.Log("Swipe lên");
                                    Move(Vector2Int.up, 0, 1, 1, 1);
                                }
                                else
                                {
                                    Move(Vector2Int.down, 0, 1, grid.height - 2, -1);
                                    Debug.Log("Swipe xuống");
                                }
                            }
                        }
                    }
                    isSwiping = false;
                    break;
            }
        }
    }

    private void Move(Vector2Int direction, int startX, int incrementX, int startY, int incrementY)
    {
        bool changed = false;

        for (int x = startX; x >= 0 && x < grid.width; x += incrementX)
        {
            for (int y = startY; y >= 0 && y < grid.height; y += incrementY)
            {
                TileCell cell = grid.GetCell(x, y);

                if (cell.occupied) {
                    changed |= MoveTile(cell.tile, direction);
                }
            }
        }

        if (changed) {
            StartCoroutine(WaitForChanges());
        }
    }

    private bool MoveTile(Tile tile, Vector2Int direction)
    {
        TileCell newCell = null;
        TileCell adjacent = grid.GetAdjacentCell(tile.cell, direction);

        while (adjacent != null)
        {
            if (adjacent.occupied)
            {
                if (CanMerge(tile, adjacent.tile))
                {
                    MergeTiles(tile, adjacent.tile);
                    return true;
                }

                break;
            }

            newCell = adjacent;
            adjacent = grid.GetAdjacentCell(adjacent, direction);
        }

        if (newCell != null)
        {
            tile.MoveTo(newCell);
            return true;
        }

        return false;
    }

    private bool CanMerge(Tile a, Tile b)
    {
        return a.state == b.state && !b.locked;
    }

    private int GenerateRandomNumber(int min, int max)
    {
        int randomInt = Random.Range(min, max);
        return randomInt;
    }

    private void MergeTiles(Tile a, Tile b)
    {
        tiles.Remove(a);
        a.Merge(b.cell);

        int indexNextTile = 0;
        int randomNumber = GenerateRandomNumber(1, 100);

        TileState nextTileState = tileStates[IndexOf(b.state) + 1];
        if (randomNumber  <= nextTileState.percentAppear + nextTileState.percentMissCount)
        {
            indexNextTile = Mathf.Clamp(IndexOf(b.state) + 1, 0, tileStates.Length - 1);
            nextTileState.percentAppear = 100;
        }
        else
        {
            nextTileState.percentMissCount++;
            indexNextTile = GenerateRandomNumber(0, IndexOf(b.state));
        }

        TileState newState = tileStates[indexNextTile];
        b.SetState(newState);
        gameManager.IncreaseScore(newState.number);
    }

    

    private int IndexOf(TileState state)
    {
        for (int i = 0; i < tileStates.Length; i++)
        {
            if (state == tileStates[i]) {
                return i;
            }
        }

        return -1;
    }

    private IEnumerator WaitForChanges()
    {
        waiting = true;

        yield return new WaitForSeconds(0.1f);

        waiting = false;

        foreach (var tile in tiles) {
            tile.locked = false;
        }

        if (tiles.Count != grid.size) {
            CreateTile();
        }

        if (CheckForGameOver()) {
            gameManager.GameOver();
        }
    }

    public bool CheckForGameOver()
    {
        if (tiles.Count != grid.size) {
            return false;
        }

        foreach (var tile in tiles)
        {
            TileCell up = grid.GetAdjacentCell(tile.cell, Vector2Int.up);
            TileCell down = grid.GetAdjacentCell(tile.cell, Vector2Int.down);
            TileCell left = grid.GetAdjacentCell(tile.cell, Vector2Int.left);
            TileCell right = grid.GetAdjacentCell(tile.cell, Vector2Int.right);

            if (up != null && CanMerge(tile, up.tile)) {
                return false;
            }

            if (down != null && CanMerge(tile, down.tile)) {
                return false;
            }

            if (left != null && CanMerge(tile, left.tile)) {
                return false;
            }

            if (right != null && CanMerge(tile, right.tile)) {
                return false;
            }
        }

        return true;
    }

}
