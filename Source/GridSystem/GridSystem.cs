﻿using System;
using System.Collections.Generic;
using FlaxEngine;

namespace GridSystem;

/// <summary>
/// GridSystem Script.
/// </summary>
public class GridSystem<TGridObject>
{

	public Vector2 Dimension { get; private set; }
	public float UnitScale { get; private set; }
	public Vector3 Origin { get; private set; }  // Store the origin

	private const float METERS_TO_CM = 100f; // Conversion factor from centimeters to meters
	public TGridObject[,] GridObjects { get; private set; }
	public int[] DirectionX;
	public int[] DirectionY;

	public GridSystem(Vector2 dimension, float unitScale, Func<GridSystem<TGridObject>, GridPosition, TGridObject> createGridObject)
	{
		Dimension = dimension;
		// Convert UnitScale from centimeters to meters
		UnitScale = unitScale * METERS_TO_CM;
		// Convert Origin from centimeters to meters
		Origin = Vector3.Zero * METERS_TO_CM;

		GridObjects = new TGridObject[(int)Dimension.X, (int)Dimension.Y];

		for (int x = 0; x < Dimension.X; x++)
		{
			for (int z = 0; z < Dimension.Y; z++)
			{
				GridPosition pos = new GridPosition(x, z);
				GridObjects[x, z] = createGridObject(this, pos);
			}
		}
		DirectionX = new int[] { 0, 1, 0, -1 };
		DirectionY = new int[] { 1, 0, -1, 0 };
	}

	public bool IsPositionValid(GridPosition position)
	{
		return IsPositionValid(position.X, position.Z);
	}

	public bool IsPositionValid(int x, int z)
	{
		return IsPositionXValid(x) && IsPositionZValid(z);
	}

	public bool IsPositionXValid(int x)
	{
		return x >= 0 && x < Dimension.X;
	}

	public bool IsPositionZValid(int z)
	{
		return z >= 0 && z < Dimension.Y;
	}

	/// <summary>
	/// Creates debug objects for the grid system
	/// </summary>
	/// <remarks>Can only be called on OnStart</remarks>
	/// <param name="prefab"></param>
	public void CreateDebugObjects(Prefab prefab)
	{
		Actor debugActor = Level.FindActor("GridDebugObjects");
		if (debugActor == null)
		{
			debugActor = new EmptyActor();
			debugActor.Name = "GridDebugObjects";
			Level.SpawnActor(debugActor);
		}

		for (int x = 0; x < Dimension.X; x++)
		{
			for (int z = 0; z < Dimension.Y; z++)
			{
				GridPosition gridPos = new GridPosition(x, z);
				Actor debugObj = PrefabManager.SpawnPrefab(prefab, GetWorldPosition(gridPos), Quaternion.Identity);
				debugObj.Parent = debugActor;
				if (!debugObj.TryGetScript<GridDebugObject>(out var gridDebugObject)) return;


				gridDebugObject.SetGridObject(GetGridObject(gridPos));
				debugObj.Name = $"GridObject_{gridPos}";

				// BoundingSphere sphere = new BoundingSphere(GetWorldPosition(gridPos), 5f); // Convert radius to meters
				// DebugDraw.DrawSphere(sphere, Color.Red, 20);

			}
		}
		DrawGridBoundingBox();
	}

	/// <summary>
	/// Converts the world size to a grid size. 
	/// Since even numbers doesnt cover the whole grid node, we add 1 to the world size to make it odd.
	/// </summary>
	/// <param name="worldSize"></param>
	/// <returns></returns>
	public int ToGridSize(int worldSize)
	{
		if (worldSize % 2 != 0) return worldSize; // If the world size is odd, return the world size
		else return worldSize + 1; // If the world size is even, return the world size + 1
	}


	public BoundingBox GetBoundingBox()
	{
		BoundingBox gridBounds = GetBoundingBox(out Vector3 minWorldPos, out Vector3 maxWorldPos);
		float halfUnitScale = UnitScale / 2;
		gridBounds.Minimum.X -= halfUnitScale;
		gridBounds.Minimum.Z -= halfUnitScale;

		gridBounds.Maximum.X += halfUnitScale;
		gridBounds.Maximum.Z += halfUnitScale;
		return gridBounds;
	}

	private BoundingBox GetBoundingBox(out Vector3 minWorldPos, out Vector3 maxWorldPos, bool isDebug = false, float yOffset = 0)
	{
		// Define grid boundaries in grid coordinates
		GridPosition min = (GridObjects[0, 0] as IGridObject).GridPosition;
		GridPosition max = (GridObjects[(int)Dimension.X - 1, (int)Dimension.X - 1] as IGridObject).GridPosition;

		// Get the world positions with the center offset
		minWorldPos = GetWorldPosition(min);
		maxWorldPos = GetWorldPosition(max);

		// Add the y offset
		if (isDebug)
		{
			minWorldPos.Y += yOffset;
			maxWorldPos.Y += yOffset;
		}


		return new BoundingBox(minWorldPos, maxWorldPos);
	}

	private void DrawGridBoundingBox()
	{
		// Create a bounding box from the world positions
		BoundingBox gridBounds = GetBoundingBox(out Vector3 minWorldPos, out Vector3 maxWorldPos);
		float halfUnitScale = UnitScale / 2;
		minWorldPos.X -= halfUnitScale;
		minWorldPos.Z -= halfUnitScale;

		maxWorldPos.X += halfUnitScale;
		maxWorldPos.Z += halfUnitScale;
		DebugDraw.DrawSphere(new BoundingSphere(minWorldPos, 5f), Color.Green, 20);
		DebugDraw.DrawSphere(new BoundingSphere(maxWorldPos, 5f), Color.Green, 20);


		gridBounds.Minimum.X -= halfUnitScale;
		gridBounds.Minimum.Z -= halfUnitScale;

		gridBounds.Maximum.X += halfUnitScale;
		gridBounds.Maximum.Z += halfUnitScale;
		// Draw the bounding box
		DebugDraw.DrawWireBox(gridBounds, Color.Beige, 10.0f);
	}

	public TGridObject GetGridObject(GridPosition position)
	{
		return GridObjects[position.X, position.Z];
	}

	private Vector3 GetOffset()
	{
		// Calculate the grid size (offset by 1 to account for zero-based indexing)
		float gridSizeX = Dimension.X - 1;
		float gridSizeZ = Dimension.Y - 1;

		const float CENTER_OFFSET = 2f; // TODO: Move to a constant of the class

		// Calculate the offset for centering
		float offsetX = gridSizeX / CENTER_OFFSET * UnitScale;
		float offsetZ = gridSizeZ / CENTER_OFFSET * UnitScale;

		return new Vector3(offsetX, 0, offsetZ);
	}

	/// <summary>
	/// Converts a world position to a grid position
	/// </summary>
	/// <remarks>Automatically converts into GridPosition</remarks>
	/// 
	/// <param name="worldPosition"></param>
	/// <returns></returns>
	public Vector3 GetWorldPosition(Vector3 worldPosition)
	{
		GridPosition gridPos = GetGridPosition(worldPosition);
		return GetWorldPosition(gridPos);
	}

	public Vector3 GetWorldPosition(GridPosition pos)
	{
		// Get the center offset
		Vector3 offset = GetOffset();

		// Calculate the world position with centering adjustments
		float scaledX = pos.X * UnitScale + (UnitScale / 2); // Add half the unit scale
		float scaledZ = pos.Z * UnitScale + (UnitScale / 2); // Add half the unit scale

		// Translate the grid position, centering the grid on the origin
		return Origin + new Vector3(scaledX - offset.X, 0, scaledZ - offset.Z);
	}



	public GridPosition GetGridPosition(Vector3 worldPosition)
	{
		return GetGridPosition(worldPosition.X, worldPosition.Z);
	}

	public GridPosition GetGridPosition(float x, float z)
	{

		// Get the center offset
		Vector3 offset = GetOffset();

		// Translate the world position back to grid coordinates
		int gridX = (int)((x - Origin.X + offset.X) / UnitScale);
		int gridZ = (int)((z - Origin.Z + offset.Z) / UnitScale);

		if (!IsPositionValid(gridX, gridZ)) return null;

		return new GridPosition(gridX, gridZ);
	}


}



public class GridPosition
{
	public int X;
	public int Z;

	public GridPosition(int x, int z)
	{
		X = x;
		Z = z;
	}

	public GridPosition(Vector3 worldPosition)
	{
		X = (int)worldPosition.X;
		Z = (int)worldPosition.Z;
	}

	public static GridPosition operator +(GridPosition a, GridPosition b)
	{
		return new GridPosition(a.X + b.X, a.Z + b.Z);
	}

	public static GridPosition operator -(GridPosition a, GridPosition b)
	{
		return new GridPosition(a.X - b.X, a.Z - b.Z);
	}

	public Vector3 ToVector3()
	{
		return new Vector3(X, 0, Z);
	}


	public override string ToString()
	{
		return $"({X}, {Z})";
	}

}
