using System;
using System.Collections.Generic;
using FlaxEngine;

namespace GridSystem;

/// <summary>
/// PathFinding Script.
/// </summary>
public class PathFinding<T> where T : PathNode<T>
{
	public GridSystem<T> GridSystem { get; private set; }

	/// <summary>
	/// Delegate for calculating the tentative G cost of a node.
	/// </summary>
	/// <param name="tentativeGCost">Index (0): Neighrbor - Index(1): Current Node - Index(2): EndNode</param>
	/// <param name="nodes"></param>
	public delegate void TentativeGCostDelegate(ref int tentativeGCost, params T[] nodes);
	public T[,] GridObjects => GridSystem.GridObjects;

	public SystemVisual<T> Visual => GridSystem.Visual;

	public PathFinding(Vector2 dimension, float unitScale, Func<GridSystem<T>, GridPosition, T> createGridObject)
	{
		GridSystem = new GridSystem<T>(dimension, unitScale, createGridObject);
		GridSystem.OnObjectOccupancyChanged += OnOccupancyChanged;

	}


	public PathFinding(int dimension, float unitScale, Func<GridSystem<T>, GridPosition, T> createGridObject)
	{
		GridSystem = new GridSystem<T>(new Vector2(dimension), unitScale, createGridObject);
		GridSystem.OnObjectOccupancyChanged += OnOccupancyChanged;

	}

	/// <summary>
	/// Toggles the neighboring nodes based of off <paramref name="Width"/> and <paramref name="Length"/>
	/// </summary>
	/// <param name="basePosition"></param>
	/// <param name="Width"></param>
	/// <param name="Length"></param>
	/// <param name="flag"></param>
	public void ToggleNeighborWalkable(GridPosition basePosition, int Width, int Length, bool flag)
	{
		List<GridPosition> positions = GetNeighborhood(basePosition, Width, Length);

		foreach (GridPosition pos in positions)
			ToggleNodeWalkable(pos, flag);
	}

	/// <summary>
	/// Returns a list of <see cref="GridPosition"/> (in all 8 directions) based of off <paramref name="Width"/> and <paramref name="Length"/>
	/// </summary>
	/// <param name="basePosition"></param>
	/// <param name="Width"></param>
	/// <param name="Length"></param>
	/// <returns></returns>
	public List<GridPosition> GetNeighborhood(GridPosition basePosition, int Width, int Length)
	{
		List<GridPosition> positions = new List<GridPosition>();
		int gridWidth = GridSystem.ToGridSize(Width);
		int gridLength = GridSystem.ToGridSize(Length);

		int widthOffset = gridWidth / 2;
		int lengthOffset = gridLength / 2;


		for (int i = 0; i < gridWidth; i++)
		{
			for (int j = 0; j < gridLength; j++)
			{
				GridPosition pos = new GridPosition(basePosition.X - widthOffset + i, basePosition.Z - lengthOffset + j);
				positions.Add(pos);
			}
		}

		return positions;
	}


	/// <summary>
	/// Returns a list of <see cref="GridPosition"/> representing the immediate neighbors (1-unit grid radius) around the base position.
	/// </summary>
	/// <param name="basePosition"></param>
	/// <returns></returns>
	public List<GridPosition> GetNeighborhood(GridPosition basePosition)
	{
		List<GridPosition> positions = new List<GridPosition>();

		// Define the relative positions for the 8 possible neighbors
		int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
		int[] dz = { -1, -1, -1, 0, 0, 1, 1, 1 };

		for (int i = 0; i < 8; i++)
		{
			GridPosition pos = new GridPosition(basePosition.X + dx[i], basePosition.Z + dz[i]);
			if (GridSystem.IsPositionValid(pos))
			{
				positions.Add(pos);
			}
		}

		return positions;
	}


	public BoundingBox GetBoundingBox()
	{
		return GridSystem.GetBoundingBox();
	}

	public void SpawnDebugObjects(Prefab debugGridPrefab)
	{
		// gridSystem.CreateDebugObjects(debugGridPrefab);
	}

	public T GetNode(int x, int z)
	{
		GridPosition position = new(x, z);
		return GetNode(position);
	}

	public T GetNode(GridPosition position)
	{
		if (!GridSystem.IsPositionValid(position)) return null;
		return GridSystem.GetGridObject(position);
	}

	/// <summary>
	/// Finds a path between two grid positions using the A* algorithm.
	/// </summary>
	/// <param name="start"></param>
	/// <param name="end"></param>
	/// <param name="startNode"></param>
	/// <param name="endNode"></param>
	/// <param name="GCostDelegate">Index (0): Neighrbor - Index(1): Current Node - Index(2): EndNode</param>
	/// <returns></returns>
	public List<GridPosition> FindPath(GridPosition start, GridPosition end, out T startNode, out T endNode, TentativeGCostDelegate GCostDelegate = null)
	{
		List<T> openList = new List<T>(); // Nodes to be evaluated
		List<T> closedList = new List<T>(); // Already visited nodes

		// Add Start node to the open list
		startNode = GetNode(start);
		endNode = GetNode(end);

		// Check if start or end node is not walkable
		if (!startNode.IsWalkable)
		{
			startNode = FindNearestWalkableNode(startNode);
			if (startNode == null)
			{
				Debug.Log("No walkable starting node found.");
				return null;
			}
		}

		if (!endNode.IsWalkable)
		{
			endNode = FindNearestWalkableNode(endNode);
			if (endNode == null)
			{
				Debug.Log("No walkable ending node found.");
				return null;
			}
		}

		openList.Add(startNode);

		if (!GridSystem.GetWorldPosition(endNode.GridPosition, out Vector3 debugEndNodePosition))
		{
			Debug.Log($"Could not get world position for {endNode.GridPosition}");
		}
		debugEndNodePosition.Y += 100f;

		// TODO: Enabled by boolean
		DebugDraw.DrawSphere(new BoundingSphere(debugEndNodePosition, 15f), Color.Azure, 60f);

		int dimensionX = (int)GridSystem.Dimension.X;
		int dimensionY = (int)GridSystem.Dimension.Y;

		// Initialize path nodes 
		// WhatIf: Convert into Parallel Processing
		for (int x = 0; x < dimensionX; x++)
		{
			for (int z = 0; z < dimensionY; z++)
			{
				GridPosition pos = new GridPosition(x, z);
				T pathNode = GetNode(pos);
				pathNode.SetGCost(int.MaxValue);
				pathNode.SetHCost(0);
				pathNode.SetPreviousNode(null);
			}
		}

		startNode.SetGCost(0);
		startNode.SetHCost(CalculateDistance(start, end));

		while (openList.Count > 0)
		{
			T currentNode = GetLowestFCostNode(openList);

			// If the current node is the end node, return the path
			if (currentNode == endNode)
			{

				return CalculatePath(endNode);
			}


			openList.Remove(currentNode);
			closedList.Add(currentNode);


			foreach (T neighbor in GetCardinalNodes(currentNode)) // Cardinal Nodes for eliminating diagonal movement
			{
				if (closedList.Contains(neighbor)) continue;

				if (!neighbor.IsWalkable)
				{
					closedList.Add(neighbor);
					continue;
				}

				// Cost from the start node to the current node
				// WhatIf: Convert this into a delegate
				int tentativeGCost = currentNode.GCost + CalculateDistance(currentNode.GridPosition, neighbor.GridPosition);

				GCostDelegate?.Invoke(ref tentativeGCost, neighbor, currentNode, endNode);


				if (tentativeGCost < neighbor.GCost)  // If the new path is shorter
				{
					// Update the neighbor node
					neighbor.SetPreviousNode(currentNode);
					neighbor.SetGCost(tentativeGCost);
					neighbor.SetHCost(CalculateDistance(neighbor.GridPosition, end));

					if (!openList.Contains(neighbor))
						openList.Add(neighbor);
				}
			}



		}

		// No path found
		Debug.Log("No path found");
		return null;
	}

	public void ClearNode(T node)
	{
		node.SetGCost(int.MaxValue);
		node.SetHCost(0);
		node.SetPreviousNode(null);
	}




	private T FindNearestWalkableNode(T node, int searchRadius = 10)
	{
		for (int radius = 1; radius <= searchRadius; radius++)
		{
			for (int x = -radius; x <= radius; x++)
			{
				for (int z = -radius; z <= radius; z++)
				{
					// Skip diagonal nodes: only check when either X or Z offset is 0
					if (Math.Abs(x) != 0 && Math.Abs(z) != 0) continue;

					GridPosition newPos = new GridPosition(node.GridPosition.X + x, node.GridPosition.Z + z);

					// Skip if the position is outside the grid bounds
					if (!GridSystem.IsPositionValid(newPos)) continue;

					// Get the neighbor node
					T neighborNode = GetNode(newPos);

					// If the neighbor is walkable, return it
					if (neighborNode != null && neighborNode.IsWalkable)
					{
						return neighborNode;
					}
				}
			}
		}

		return null; // No walkable node found within the search radius
	}


	/// <summary>
	/// Returns a list of neighboring nodes in the cardinal directions (up, down, left, right)
	/// </summary>
	/// <param name="node"></param>
	/// <returns></returns>
	public List<T> GetCardinalNodes(T node)
	{
		List<T> neighboringNodes = new List<T>();

		GridPosition position = node.GridPosition;

		if (GridSystem.IsPositionXValid(position.X - 1))
			neighboringNodes.Add(GetNode(position.X - 1, position.Z)); // Left

		if (GridSystem.IsPositionXValid(position.X + 1))
			neighboringNodes.Add(GetNode(position.X + 1, position.Z)); // Right

		if (GridSystem.IsPositionZValid(position.Z - 1))
			neighboringNodes.Add(GetNode(position.X, position.Z - 1)); // Down

		if (GridSystem.IsPositionZValid(position.Z + 1))
			neighboringNodes.Add(GetNode(position.X, position.Z + 1)); // Up

		return neighboringNodes;
	}


	/// <summary>
	/// Returns a list of neighboring nodes in the cardinal directions (up, down, left, right)
	/// </summary>
	/// <param name="position"></param>
	/// <returns></returns>
	public List<T> GetCardinalNodes(GridPosition position)
	{
		List<T> neighboringNodes = new List<T>();


		if (GridSystem.IsPositionXValid(position.X - 1))
			neighboringNodes.Add(GetNode(position.X - 1, position.Z)); // Left

		if (GridSystem.IsPositionXValid(position.X + 1))
			neighboringNodes.Add(GetNode(position.X + 1, position.Z)); // Right

		if (GridSystem.IsPositionZValid(position.Z - 1))
			neighboringNodes.Add(GetNode(position.X, position.Z - 1)); // Down

		if (GridSystem.IsPositionZValid(position.Z + 1))
			neighboringNodes.Add(GetNode(position.X, position.Z + 1)); // Up

		return neighboringNodes;
	}
	/// <summary>
	/// Returns a list of neighboring nodes in the edge directions (North-East, North-West, South-West, South-East)
	/// </summary>
	/// <param name="node"></param>
	/// <returns></returns>
	public List<T> GetCornerNodes(T node)
	{
		List<T> neighboringNodes = new List<T>();

		GridPosition position = node.GridPosition;

		// Check North-East
		if (GridSystem.IsPositionXValid(position.X + 1) && GridSystem.IsPositionZValid(position.Z + 1))
			neighboringNodes.Add(GetNode(position.X + 1, position.Z + 1));

		// Check North-West
		if (GridSystem.IsPositionXValid(position.X - 1) && GridSystem.IsPositionZValid(position.Z + 1))
			neighboringNodes.Add(GetNode(position.X - 1, position.Z + 1));

		// Check South-West
		if (GridSystem.IsPositionXValid(position.X - 1) && GridSystem.IsPositionZValid(position.Z - 1))
			neighboringNodes.Add(GetNode(position.X - 1, position.Z - 1));

		// Check South-East
		if (GridSystem.IsPositionXValid(position.X + 1) && GridSystem.IsPositionZValid(position.Z - 1))
			neighboringNodes.Add(GetNode(position.X + 1, position.Z - 1));

		return neighboringNodes;
	}

	private List<GridPosition> CalculatePath(T endNode)
	{
		List<T> path = [endNode];

		T currentNode = endNode; // Starting from the end node
		while (currentNode.PreviousNode != null)
		{
			path.Add(currentNode.PreviousNode);
			currentNode = currentNode.PreviousNode;
		}

		path.Reverse();

		List<GridPosition> gridPath = new List<GridPosition>();
		foreach (T node in path)
		{
			gridPath.Add(node.GridPosition);
		}

		return gridPath;
	}

	private T GetLowestFCostNode(List<T> openList)
	{
		T lowestFCostNode = openList[0];
		for (int i = 1; i < openList.Count; i++)
		{
			if (openList[i].FCost < lowestFCostNode.FCost)
				lowestFCostNode = openList[i];
		}
		return lowestFCostNode;
	}

	private int CalculateDistance(GridPosition a, GridPosition b)
	{
		// TODO: Implement a better heuristic
		GridPosition gridPosDistance = a - b;
		int xDistance = Math.Abs(gridPosDistance.X);
		int zDistance = Math.Abs(gridPosDistance.Z);
		int remaining = Math.Abs(xDistance - zDistance);

		return xDistance + zDistance;
	}

	private void ToggleNodeWalkable(GridPosition position, bool flag)
	{
		if (!GridSystem.IsPositionValid(position)) return;
		GetNode(position)?.SetWalkable(flag);
	}

	public GridPosition GetGridPosition(Vector3 position)
	{
		return GridSystem.GetGridPosition(position);
	}

	public bool GetWorldPosition(GridPosition position, out Vector3 worldPosition)
	{
		return GridSystem.GetWorldPosition(position, out worldPosition);
	}

	public bool GetWorldPosition(Vector3 position, out Vector3 worldPosition)
	{
		return GridSystem.GetWorldPosition(position, out worldPosition);
	}

	public float GetHalfUnitScale()
	{
		return GridSystem.UnitScale / 2;
	}


	private void OnOccupancyChanged(object sender, GridSystem<T>.OnObjectOccupancyChangedEventArgs e)
	{
		ToggleNeighborWalkable(e.Object.GridPosition, 1, 1, e.Object.IsOccupied);
	}

	public int[] GetDirectionX() => GridSystem.DirectionX;
	public int[] GetDirectionZ() => GridSystem.DirectionY;
	public void OnDisable()
	{
		GridSystem.OnObjectOccupancyChanged -= OnOccupancyChanged;
		GridSystem.OnDisable();
	}

	public void CreateDebugObjects(Prefab prefab, float yOffset = 0) => GridSystem.Visual.CreateDebugObjects(prefab, yOffset);
	public void VisualizeGrid(Prefab prefab) => GridSystem.Visual.VisualizeGrid(prefab);
	public void ChangeGridObjectOccupancy(GridPosition position, bool flag) => GridSystem.ChangeGridObjectOccupancy(position, flag);
	public T GetGridObject(GridPosition position) => GridSystem.GetGridObject(position);
	public List<GridPosition> GetOuterNodes(GridPosition gridPosition, int wdith, int length) => GridSystem.GetOuterNodes(gridPosition, wdith, length);
	public List<GridPosition> GetInnerNodes(GridPosition gridPosition, int wdith, int length) => GridSystem.GetInnerNodes(gridPosition, wdith, length);

	public void ShowVisual() => GridSystem.Visual.ShowVisuals();
	public void HideVisual() => GridSystem.Visual.HideVisuals();


}
