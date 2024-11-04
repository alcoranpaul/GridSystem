using System;
using System.Collections.Generic;
using FlaxEngine;

namespace GridSystem;

/// <summary>
/// Represents a grid system that holds objects of type <typeparamref name="TGridObject"/>.
/// </summary>
/// <typeparam name="TGridObject">The type of objects that the grid will hold.</typeparam>
public class GridSystem<TGridObject> where TGridObject : GridObject<TGridObject>
{
	public Vector2 Dimension { get; private set; }
	public float UnitScale { get; private set; }
	public Vector3 Origin { get; private set; }  // Store the origin

	private const float METERS_TO_CM = 100f; // Conversion factor from centimeters to meters

	public TGridObject[,] GridObjects { get; private set; }
	public SystemVisual<TGridObject> Visual { get; private set; }

	public event EventHandler<OnObjectOccupancyChangedEventArgs> OnObjectOccupancyChanged;
	public class OnObjectOccupancyChangedEventArgs : EventArgs
	{
		public TGridObject Object;
	}

	public int[] DirectionX;
	public int[] DirectionY;

	/// <summary>
	/// Initializes a new instance of the <see cref="GridSystem{TGridObject}"/> class.
	/// </summary>
	/// <param name="dimension">The dimensions of the grid.</param>
	/// <param name="unitScale">The scale of each grid unit.</param>
	/// <param name="createGridObject">A function to create grid objects.</param>
	public GridSystem(Vector2 dimension, float unitScale, Func<GridSystem<TGridObject>, GridPosition, TGridObject> createGridObject)
	{
		Dimension = dimension;
		// Convert UnitScale from centimeters to meters
		UnitScale = unitScale * METERS_TO_CM;
		// Convert Origin from centimeters to meters
		Origin = Vector3.Zero * METERS_TO_CM;

		// Initialize the grid objects array and dictionary
		GridObjects = new TGridObject[(int)Dimension.X, (int)Dimension.Y];


		// Populate the grid with objects
		for (int x = 0; x < Dimension.X; x++)
		{
			for (int z = 0; z < Dimension.Y; z++)
			{
				GridPosition pos = new GridPosition(x, z);
				GridObjects[x, z] = createGridObject(this, pos);

			}
		}

		Visual = new SystemVisual<TGridObject>(this);

		// Initialize direction arrays for grid navigation
		DirectionX = new int[] { 0, 1, 0, -1 };
		DirectionY = new int[] { 1, 0, -1, 0 };
	}

	/// <summary>
	///  Iterates through the grid and performs the specified action on each grid object.
	/// </summary>
	/// <param name="action">The action to perform on each grid object.</param>
	/// <returns></returns>
	public bool IterateThroughGrid(Action<TGridObject> action)
	{
		if (action == null)
		{
			Debug.LogWarning("Action is null. Please provide a valid action.");
			return false;
		}

		// Iterate through each grid position and perform the action
		for (int x = 0; x < Dimension.X; x++)
		{
			for (int z = 0; z < Dimension.Y; z++)
			{
				action(GridObjects[x, z]);
			}
		}
		return true;
	}


	/// <summary>
	/// Checks if the specified grid position is valid.
	/// </summary>
	/// <param name="position">The grid position to check.</param>
	/// <returns>True if the position is valid, otherwise false.</returns>
	public bool IsPositionValid(GridPosition position)
	{
		return IsPositionValid(position.X, position.Z);
	}

	/// <summary>
	/// Checks if the specified grid coordinates are valid.
	/// </summary>
	/// <param name="x">The x-coordinate to check.</param>
	/// <param name="z">The z-coordinate to check.</param>
	/// <returns>True if the coordinates are valid, otherwise false.</returns>
	public bool IsPositionValid(int x, int z)
	{
		return IsPositionXValid(x) && IsPositionZValid(z);
	}

	/// <summary>
	/// Checks if the specified x-coordinate is valid.
	/// </summary>
	/// <param name="x">The x-coordinate to check.</param>
	/// <returns>True if the x-coordinate is valid, otherwise false.</returns>
	public bool IsPositionXValid(int x)
	{
		return x >= 0 && x < Dimension.X;
	}

	/// <summary>
	/// Checks if the specified z-coordinate is valid.
	/// </summary>
	/// <param name="z">The z-coordinate to check.</param>
	/// <returns>True if the z-coordinate is valid, otherwise false.</returns>
	public bool IsPositionZValid(int z)
	{
		return z >= 0 && z < Dimension.Y;
	}

	/// <summary>
	/// Gets the grid object at the specified grid position.
	/// </summary>
	/// <param name="position">The grid position.</param>
	/// <returns>The grid object at the specified position.</returns>
	public TGridObject GetGridObject(GridPosition position)
	{
		if (position == null || !IsPositionValid(position)) return null;
		return GridObjects[position.X, position.Z];
	}


	/// <summary>
	/// Converts the world size to a grid size.
	/// Since even numbers don't cover the whole grid node, we add 1 to the world size to make it odd.
	/// </summary>
	/// <param name="worldSize">The world size to convert.</param>
	/// <returns>The converted grid size.</returns>
	public int ToGridSize(int worldSize)
	{
		if (worldSize % 2 != 0) return worldSize; // If the world size is odd, return the world size
		else return worldSize + 1; // If the world size is even, return the world size + 1
	}

	/// <summary>
	/// Gets the bounding box of the grid.
	/// </summary>
	/// <returns>The bounding box of the grid.</returns>
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

	/// <summary>
	/// Converts a world position to a grid position.
	/// </summary>
	/// <param name="worldPosition">The world position to convert.</param>
	/// <returns>The corresponding grid position.</returns>
	public GridPosition GetGridPosition(Vector3 worldPosition)
	{
		return GetGridPosition(worldPosition.X, worldPosition.Z);
	}

	/// <summary>
	/// Converts world coordinates to a grid position.
	/// </summary>
	/// <param name="x">The x-coordinate in the world.</param>
	/// <param name="z">The z-coordinate in the world.</param>
	/// <returns>The corresponding grid position.</returns>
	public GridPosition GetGridPosition(float x, float z)
	{
		// Get the center offset
		Vector3 offset = GetOffset();

		// Translate the world position back to grid coordinates
		int gridX = (int)((x - Origin.X + offset.X) / UnitScale);
		int gridZ = (int)((z - Origin.Z + offset.Z) / UnitScale);

		// Check if the calculated grid position is valid
		if (!IsPositionValid(gridX, gridZ)) return null;

		return new GridPosition(gridX, gridZ);
	}

	/// <summary>
	/// Converts a world position to a grid position.
	/// </summary>
	/// <remarks>Automatically converts into GridPosition</remarks>
	/// <param name="worldPosition">The world position to convert.</param>
	/// <param name="convertedWorldPosition"></param>
	/// <returns>The corresponding world position.</returns>
	public bool GetWorldPosition(Vector3 worldPosition, out Vector3 convertedWorldPosition)
	{
		GridPosition gridPos = GetGridPosition(worldPosition);
		if (gridPos == null || !GetWorldPosition(gridPos, out convertedWorldPosition))
		{
			convertedWorldPosition = Vector3.Zero;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Converts a grid position to a world position.
	/// </summary>
	/// <param name="pos">The grid position to convert.</param>
	/// <param name="worldPosition"></param>
	/// <returns>The corresponding world position.</returns>
	public bool GetWorldPosition(GridPosition pos, out Vector3 worldPosition)
	{
		if (pos == null)
		{
			worldPosition = Vector3.Zero;
			return false;
		}

		// Get the center offset
		Vector3 offset = GetOffset();

		// Calculate the world position with centering adjustments
		float scaledX = pos.X * UnitScale + (UnitScale / 2); // Add half the unit scale
		float scaledZ = pos.Z * UnitScale + (UnitScale / 2); // Add half the unit scale

		// Translate the grid position, centering the grid on the origin
		worldPosition = Origin + new Vector3(scaledX - offset.X, 0, scaledZ - offset.Z);
		return true;
	}

	/// <summary>
	/// Gets the bounding box of the grid.
	/// </summary>
	/// <param name="minWorldPos">The minimum world position of the bounding box.</param>
	/// <param name="maxWorldPos">The maximum world position of the bounding box.</param>
	/// <param name="isDebug">Indicates whether this is for debugging purposes.</param>
	/// <param name="yOffset">The y-offset to apply.</param>
	/// <returns>The bounding box of the grid.</returns>
	public BoundingBox GetBoundingBox(out Vector3 minWorldPos, out Vector3 maxWorldPos, bool isDebug = false, float yOffset = 0)
	{
		// Define grid boundaries in grid coordinates
		GridPosition min = (GridObjects[0, 0] as IGridObject).GridPosition;
		GridPosition max = (GridObjects[(int)Dimension.X - 1, (int)Dimension.X - 1] as IGridObject).GridPosition;

		// Get the world positions with the center offset
		if (!GetWorldPosition(min, out minWorldPos) || !GetWorldPosition(max, out maxWorldPos))
		{
			Debug.LogError("Failed to get world position");
			maxWorldPos = Vector3.Zero;
			return new BoundingBox(Vector3.Zero, Vector3.Zero);
		}


		// Add the y offset if debugging
		if (isDebug)
		{
			minWorldPos.Y += yOffset;
			maxWorldPos.Y += yOffset;
		}

		return new BoundingBox(minWorldPos, maxWorldPos);
	}

	/// <summary>
	/// Toggles the grid object's occupancy at the specified grid position.
	/// </summary>
	/// <param name="gridPosition"></param>
	/// <param name="flag"></param>
	public void ChangeGridObjectOccupancy(GridPosition gridPosition, bool flag)
	{
		if (!IsPositionValid(gridPosition))
		{
			Debug.LogWarning("Invalid grid position.");
			return;
		}

		TGridObject gridObject = GetGridObject(gridPosition);
		if (flag)
			gridObject.Occupy();
		else
			gridObject.Vacate();

		OnObjectOccupancyChanged?.Invoke(this, new OnObjectOccupancyChangedEventArgs { Object = gridObject });

	}

	/// <summary>
	/// Toggles the grid object's occupancy at the specified grid position.
	/// </summary>
	/// <param name="worldPosition"></param>
	/// <param name="flag"></param>
	public void ChangeGridObjectOccupancy(Vector3 worldPosition, bool flag)
	{
		GridPosition gridPosition = GetGridPosition(worldPosition);
		if (gridPosition == null)
		{
			Debug.LogWarning("Invalid grid position.");
			return;
		}
		ChangeGridObjectOccupancy(gridPosition, flag);
	}

	/// <summary>
	/// Returns a list of <see cref="GridPosition"/> representing the outer nodes (most edges) based on <paramref name="Width"/> and <paramref name="Length"/>
	/// </summary>
	/// <param name="basePosition">The base position around which the outer nodes are calculated.</param>
	/// <param name="Width">The width of the grid area.</param>
	/// <param name="Length">The length of the grid area.</param>
	/// <returns>A list of <see cref="GridPosition"/> representing the outer nodes.</returns>
	public List<GridPosition> GetOuterNodes(GridPosition basePosition, int Width, int Length)
	{
		// Initialize the list to hold the positions of the outer nodes
		List<GridPosition> positions = new List<GridPosition>();

		// Convert the width and length to grid sizes
		int gridWidth = ToGridSize(Width);
		int gridLength = ToGridSize(Length);

		// Calculate the offsets to center the grid around the base position
		int widthOffset = gridWidth / 2;
		int lengthOffset = gridLength / 2;

		if (Dimension.X - gridWidth < 0) gridWidth = (int)Dimension.X;
		if (Dimension.Y - gridLength < 0) gridLength = (int)Dimension.Y;
		// Iterate through the grid dimensions
		for (int i = 0; i < gridWidth; i++)
		{
			for (int j = 0; j < gridLength; j++)
			{
				// Include only the outer nodes (first and last rows and columns)
				if (i == 0 || i == gridWidth - 1 || j == 0 || j == gridLength - 1)
				{
					int x = basePosition.X - widthOffset + i;
					int z = basePosition.Z - lengthOffset + j;
					if (x < 0) x = 0;
					else if (x >= Dimension.X) x = (int)Dimension.X - 1;
					if (z < 0) z = 0;
					else if (z >= Dimension.Y) z = (int)Dimension.Y - 1;

					// Calculate the grid position based on the base position and offsets
					GridPosition pos = new GridPosition(x, z);

					// Check if the position is valid within the grid system
					if (!IsPositionValid(pos)) continue;

					// Add the valid outer node position to the list
					positions.Add(pos);
				}
			}
		}

		// Return the list of outer node positions
		return positions;
	}

	/// <summary>
	/// Returns a list of <see cref="GridPosition"/> representing the inner nodes excluding the outer nodes based on <paramref name="Width"/> and <paramref name="Length"/>
	/// </summary>
	/// <param name="basePosition"></param>
	/// <param name="Width"></param>
	/// <param name="Length"></param>
	/// <returns></returns>
	public List<GridPosition> GetInnerNodes(GridPosition basePosition, int Width, int Length)
	{
		List<GridPosition> positions = new List<GridPosition>();
		int gridWidth = ToGridSize(Width);
		int gridLength = ToGridSize(Length);

		int widthOffset = gridWidth / 2;
		int lengthOffset = gridLength / 2;

		if (Dimension.X - gridWidth < 0) gridWidth = (int)Dimension.X;
		if (Dimension.Y - gridLength < 0) gridLength = (int)Dimension.Y;
		for (int i = 1; i < gridWidth - 1; i++)
		{
			for (int j = 1; j < gridLength - 1; j++)
			{
				int x = basePosition.X - widthOffset + i;
				int z = basePosition.Z - lengthOffset + j;
				if (x < 0) x = 0;
				else if (x >= Dimension.X) x = (int)Dimension.X - 1;
				if (z < 0) z = 0;
				else if (z >= Dimension.Y) z = (int)Dimension.Y - 1;

				GridPosition pos = new GridPosition(basePosition.X - widthOffset + i, basePosition.Z - lengthOffset + j);
				positions.Add(pos);
			}
		}

		return positions;
	}


	/// <summary>
	/// Disposes of the grid system.
	/// </summary>
	public void OnDisable()
	{
		Visual.OnDisable();
	}




	/// <summary>
	/// Gets the offset for centering the grid.
	/// </summary>
	/// <returns>The offset for centering the grid.</returns>
	private Vector3 GetOffset()
	{
		// Calculate the grid size (offset by 1 to account for zero-based indexing)
		float gridSizeX = Dimension.X - 1;
		float gridSizeZ = Dimension.Y - 1;

		float centerOffset = 2f;

		// Calculate the offset for centering
		float offsetX = gridSizeX / centerOffset * UnitScale;
		float offsetZ = gridSizeZ / centerOffset * UnitScale;

		return new Vector3(offsetX, 0, offsetZ);
	}


}

/// <summary>
/// Represents a position in the grid system.
/// </summary>
public class GridPosition
{
	/// <summary>
	/// The x-coordinate of the grid position.
	/// </summary>
	public int X;
	/// <summary>
	/// The z-coordinate of the grid position.
	/// </summary>
	public int Z;

	/// <summary>
	/// Initializes a new instance of the <see cref="GridPosition"/> class.
	/// </summary>
	/// <param name="x">The x-coordinate of the grid position.</param>
	/// <param name="z">The z-coordinate of the grid position.</param>
	public GridPosition(int x, int z)
	{
		X = x;
		Z = z;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="GridPosition"/> class from a world position.
	/// </summary>
	/// <param name="worldPosition">The world position.</param>
	public GridPosition(Vector3 worldPosition)
	{
		X = (int)worldPosition.X;
		Z = (int)worldPosition.Z;
	}

	/// <summary>
	/// Adds two grid positions.
	/// </summary>
	/// <param name="a">The first grid position.</param>
	/// <param name="b">The second grid position.</param>
	/// <returns>The sum of the two grid positions.</returns>
	public static GridPosition operator +(GridPosition a, GridPosition b)
	{
		return new GridPosition(a.X + b.X, a.Z + b.Z);
	}

	/// <summary>
	/// Subtracts one grid position from another.
	/// </summary>
	/// <param name="a">The first grid position.</param>
	/// <param name="b">The second grid position.</param>
	/// <returns>The difference between the two grid positions.</returns>
	public static GridPosition operator -(GridPosition a, GridPosition b)
	{
		return new GridPosition(a.X - b.X, a.Z - b.Z);
	}

	/// <summary>
	/// Converts the grid position to a Vector3.
	/// </summary>
	/// <returns>The Vector3 representation of the grid position.</returns>
	public Vector3 ToVector3()
	{
		return new Vector3(X, 0, Z);
	}

	// <inheritdoc/>
	public override string ToString()
	{
		return $"({X}, {Z})";
	}
}