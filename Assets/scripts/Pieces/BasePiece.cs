using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public abstract class BasePiece : EventTrigger
{
    [HideInInspector]
    public Color mColor = Color.clear;
    public bool mIsFirstMove = true;

    protected Cell mOriginalCell = null;
    protected Cell mCurrentCell = null;

    protected RectTransform mRectTransform = null;
    protected PieceManager mPieceManager;

    protected Cell mTargetCell = null;

    protected Vector3Int mMovement = Vector3Int.one;
    protected List<Cell> mHighlightedCells = new List<Cell>();

    public virtual void Setup(Color newTeamColor, Color32 newSpriteColor, PieceManager newPieceManager)
    {
        mPieceManager = newPieceManager;

        mColor = newTeamColor;
        GetComponent<Image>().color = newSpriteColor;
        mRectTransform = GetComponent<RectTransform>();
    }

    public virtual void Place(Cell newCell)
    {
        // Cell stuff
        mCurrentCell = newCell;
        mOriginalCell = newCell;
        mCurrentCell.mCurrentPiece = this;

        // Object stuff
        transform.position = newCell.transform.position;
        gameObject.SetActive(true);
    }

    public void Reset()
    {
        Kill();

        mIsFirstMove = true;

        Place(mOriginalCell);
    }

    public virtual void Kill()
    {
        // Clear current cell
        mCurrentCell.mCurrentPiece = null;

        // Remove piece
        gameObject.SetActive(false);
    }

    public bool HasMove()
    {
        CheckPathing();

        // If no moves
        if (mHighlightedCells.Count == 0)
            return false;

        // If moves available
        return true;
    }

    public void ComputerMove()
    {
        // Get random cell
        int i = Random.Range(0, mHighlightedCells.Count);
        mTargetCell = mHighlightedCells[i];

        // Move to new cell
        Move();

        // End turn
        mPieceManager.SwitchSides(mColor);
    }

    #region Movement
    private void CreateCellPath(int xDirection, int yDirection, int movement)
    {
        // Target position
        int currentX = mCurrentCell.mBoardPosition.x;
        int currentY = mCurrentCell.mBoardPosition.y;

        // Check each cell
        for (int i = 1; i <= movement; i++)
        {
            currentX += xDirection;
            currentY += yDirection;

            // Get the state of the target cell
            CellState cellState = CellState.None;
            cellState = mCurrentCell.mBoard.ValidateCell(currentX, currentY, this);

            // If enemy, add to list, break
            if (cellState == CellState.Enemy)
            {
                mHighlightedCells.Add(mCurrentCell.mBoard.mAllCells[currentX, currentY]);
                break;
            }

            // If the cell is not free, break
            if (cellState != CellState.Free)
                break;

            // Add to list
            mHighlightedCells.Add(mCurrentCell.mBoard.mAllCells[currentX, currentY]);
        }
    }

    protected virtual void CheckPathing()
    {
        // Horizontal
        CreateCellPath(1, 0, mMovement.x);
        CreateCellPath(-1, 0, mMovement.x);

        // Vertical 
        CreateCellPath(0, 1, mMovement.y);
        CreateCellPath(0, -1, mMovement.y);

        // Upper diagonal
        CreateCellPath(1, 1, mMovement.z);
        CreateCellPath(-1, 1, mMovement.z);

        // Lower diagonal
        CreateCellPath(-1, -1, mMovement.z);
        CreateCellPath(1, -1, mMovement.z);
    }

    protected void ShowCells()
    {
        foreach (Cell cell in mHighlightedCells)
            cell.mOutlineImage.enabled = true;
    }

    protected void ClearCells()
    {
        foreach (Cell cell in mHighlightedCells)
            cell.mOutlineImage.enabled = false;

        mHighlightedCells.Clear();
    }

        protected virtual void Move()
        {
            // Validate mTargetCell
            if (mTargetCell == null)
            {
                Debug.LogError("mTargetCell is null. Cannot execute Move.");
                return;
            }

            // Validate mCurrentCell and its board
            if (mCurrentCell == null || mCurrentCell.mBoard == null)
            {
                Debug.LogError("mCurrentCell or mCurrentCell.mBoard is null. Cannot execute Move.");
                return;
            }

            // Check if moving to y=2 or y=3
            if (mTargetCell.mBoardPosition.y == 2 || mTargetCell.mBoardPosition.y == 3)
            {
                // Adjust the target cell to y=1 while keeping x the same
                Vector2Int newPosition = mTargetCell.mBoardPosition;
                newPosition.y = 1; // Force the piece to move to y=1

                // Validate new position
                if (newPosition.x >= 0 && newPosition.x < mCurrentCell.mBoard.mAllCells.GetLength(0) &&
                    newPosition.y >= 0 && newPosition.y < mCurrentCell.mBoard.mAllCells.GetLength(1))
                    {
                        mTargetCell = mCurrentCell.mBoard.mAllCells[newPosition.x, newPosition.y];
                    }
                else
                {
                    Debug.LogError($"Invalid target position after adjustment: x={newPosition.x}, y={newPosition.y}");
                    return;
                }
            }

            // If there is an enemy piece at the new target, remove it
            mTargetCell.RemovePiece();

            // Clear the current cell
            mCurrentCell.mCurrentPiece = null;

            // Switch cells
            mCurrentCell = mTargetCell;
            mCurrentCell.mCurrentPiece = this;

            // Update the position on the board
            transform.position = mCurrentCell.transform.position;

            // Reset the target cell
            mTargetCell = null;
        }


    #endregion

    #region Events
    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);

        // Test for cells
        CheckPathing();

        // Show valid cells
        ShowCells();
    }

    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);

        // Follow pointer
        transform.position += (Vector3)eventData.delta;

        // Check for overlapping available squares
        foreach (Cell cell in mHighlightedCells)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(cell.mRectTransform, Input.mousePosition))
            {
                // If the mouse is within a valid cell, get it, and break.
                mTargetCell = cell;
                break;
            }

            // If the mouse is not within any highlighted cell, we don't have a valid move.
            mTargetCell = null;
        }
    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);

        // Hide
        ClearCells();

        // Return to original position
        if (!mTargetCell)
        {
            transform.position = mCurrentCell.gameObject.transform.position;
            return;
        }

        // Move to new cell
        Move();

        // End turn
        mPieceManager.SwitchSides(mColor);
    }
    #endregion
}
