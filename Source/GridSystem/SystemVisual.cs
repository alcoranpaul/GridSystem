using System;
using System.Collections.Generic;
using FlaxEngine;

namespace GridSystem;

/// <summary>
/// Manages the visual representation of the grid system.
/// </summary>
/// <typeparam name="TGridObject">The type of objects that the grid will hold.</typeparam>
public class SystemVisual<TGridObject> where TGridObject : GridObject<TGridObject>
{
	private GridSystem<TGridObject> gridSystem;
	private const string VISUAL_PARENT_NAME = "GridVisualizations";

	// Dictionary to map grid positions to their visual actors
	private Dictionary<GridPosition, Actor> visualDict;
	private bool isVisible;

	/// <summary>
	/// Initializes a new instance of the <see cref="SystemVisual{TGridObject}"/> class.
	/// </summary>
	/// <param name="gridSystem">The grid system to visualize.</param>
	public SystemVisual(GridSystem<TGridObject> gridSystem)
	{
		this.gridSystem = gridSystem;
	}



	/// <summary>
	/// Shows the visual representations of the grid.
	/// </summary>
	public void ShowVisuals()
	{
		if (visualDict == null || visualDict.Count == 0)
		{
			Debug.LogWarning("Visuals are not initialized. Call VisualizeGrid() first.");
			return;
		}

		foreach (KeyValuePair<GridPosition, Actor> visual in visualDict)
		{
			TGridObject gridObject = gridSystem.GetGridObject(visual.Key);
			if (!ActivateVisual(visual.Value, gridObject)) continue;
		}
		isVisible = true;
	}

	/// <summary>
	/// Shows the visual representations of the grid at the specified positions.
	/// </summary>
	/// <param name="gridPositions">The collection of grid positions to show visuals for.</param>
	public void ShowVisuals(IEnumerable<GridPosition> gridPositions)
	{
		if (visualDict == null || visualDict.Count == 0)
		{
			Debug.LogWarning("Visuals are not initialized. Call VisualizeGrid() first.");
			return;
		}

		foreach (GridPosition gridPosition in gridPositions)
		{
			if (visualDict.TryGetValue(gridPosition, out var visual))
			{
				TGridObject gridObject = gridSystem.GetGridObject(gridPosition);

				if (!ActivateVisual(visual, gridObject)) continue;
			}
		}
		isVisible = true;
	}

	/// <summary>
	/// Activates the visual representation of the grid object.
	/// </summary>
	/// <param name="visual"></param>
	/// <param name="gridObject"></param>
	/// <returns></returns>
	private static bool ActivateVisual(Actor visual, TGridObject gridObject)
	{
		if (gridObject == null || gridObject.IsOccupied) return false;

		visual.IsActive = true;
		return true;
	}

	/// <summary>
	/// Hides the visual representations of the grid.
	/// </summary>
	public void HideVisuals()
	{
		if (visualDict == null || visualDict.Count == 0)
		{
			Debug.LogWarning("Visuals are not initialized. Call VisualizeGrid() first.");
			return;
		}

		foreach (KeyValuePair<GridPosition, Actor> visual in visualDict)
		{
			visual.Value.IsActive = false;
		}
		isVisible = false;
	}

	/// <summary>
	/// Creates debug objects for the grid system.
	/// </summary>
	/// <remarks>Can only be called on OnStart</remarks>
	/// <param name="prefab">The prefab to use for the debug objects.</param>
	/// <param name="yOffset"></param>
	public void CreateDebugObjects(Prefab prefab, float yOffset = 0)
	{
		// Find or create the parent actor for debug objects
		Actor debugActor = Level.FindActor("GridDebugObjects");

		if (debugActor == null)
		{
			debugActor = new EmptyActor();
			debugActor.Name = "GridDebugObjects";
			debugActor.Position = new Vector3(0, yOffset, 0);
			Level.SpawnActor(debugActor);
		}

		// Create debug objects for each grid position
		Action<TGridObject> action = (gridobject) =>
		{
			GridPosition gridPos = gridobject.GridPosition;
			if (!gridSystem.GetWorldPosition(gridPos, out Vector3 worldPos))
			{
				Debug.LogError("Failed to get world position");
			}
			Actor debugObj = PrefabManager.SpawnPrefab(prefab, worldPos, Quaternion.Identity);
			debugObj.Parent = debugActor;
			if (!debugObj.TryGetScript<GridDebugObject>(out var gridDebugObject)) return;

			gridDebugObject.SetGridObject(gridobject);
			debugObj.Name = $"GridObject_{gridPos}";
		};
		gridSystem.IterateThroughGrid(action);
		DrawGridBoundingBox();
	}

	/// <summary>
	/// Visualizes the grid using the specified prefab.
	/// </summary>
	/// <param name="visualizePrefab">The prefab to use for visualization.</param>
	/// <param name="yOffset"></param>
	public void VisualizeGrid(Prefab visualizePrefab, float yOffset = 0)
	{
		visualDict = new Dictionary<GridPosition, Actor>();
		if (!VisualNullChecker(visualizePrefab, out Actor gridVisualizations))
		{
			Debug.LogWarning($"Failed to spawn {VISUAL_PARENT_NAME} actor. Do not run this method on Awake");
			return;
		}

		// Action to visualize each occupied grid object
		Action<TGridObject> action = (gridobject) =>
		{
			if (!gridobject.IsOccupied)
			{
				if (!gridSystem.GetWorldPosition(gridobject.GridPosition, out Vector3 worldPos))
				{
					Debug.LogError("Failed to get world position");
				}
				Actor actor = PrefabManager.SpawnPrefab(visualizePrefab, worldPos);
				actor.Name = $"Visual_{gridobject.GridPosition}";
				actor.Parent = gridVisualizations;
				visualDict[gridobject.GridPosition] = actor; // Store the visual actor in the dictionary
				actor.IsActive = false;
			}
			gridobject.OnOccupiedChanged += OnGridObjectOccupiedChanged;
		};
		gridSystem.IterateThroughGrid(action);
	}

	/// <summary>
	/// Disables the visualization of the grid.
	/// </summary>
	public void OnDisable()
	{
		// Action to visualize each occupied grid object
		Action<TGridObject> action = (gridobject) =>
		{
			gridobject.OnOccupiedChanged -= OnGridObjectOccupiedChanged;
		};
		gridSystem.IterateThroughGrid(action);
	}

	private void OnGridObjectOccupiedChanged(object sender, GridObject<TGridObject>.OnOccupiedEventArgs e)
	{
		if (sender is not TGridObject gridObject) return;
		if (visualDict.TryGetValue(gridObject.GridPosition, out var visual))
		{
			if (isVisible)
				visual.IsActive = !e.Flag;
		}
	}


	/// <summary>
	/// Draws the bounding box of the grid for debugging purposes.
	/// </summary>
	private void DrawGridBoundingBox()
	{
		// Create a bounding box from the world positions
		BoundingBox gridBounds = gridSystem.GetBoundingBox(out Vector3 minWorldPos, out Vector3 maxWorldPos);
		float halfUnitScale = gridSystem.UnitScale / 2;
		minWorldPos.X -= halfUnitScale;
		minWorldPos.Z -= halfUnitScale;

		maxWorldPos.X += halfUnitScale;
		maxWorldPos.Z += halfUnitScale;

		// Draw spheres at the corners of the bounding box
		DebugDraw.DrawSphere(new BoundingSphere(minWorldPos, 5f), Color.Green, 20);
		DebugDraw.DrawSphere(new BoundingSphere(maxWorldPos, 5f), Color.Green, 20);

		// Adjust the bounding box for visualization
		gridBounds.Minimum.X -= halfUnitScale;
		gridBounds.Minimum.Z -= halfUnitScale;

		gridBounds.Maximum.X += halfUnitScale;
		gridBounds.Maximum.Z += halfUnitScale;

		// Draw the bounding box
		DebugDraw.DrawWireBox(gridBounds, Color.Beige, 10.0f);
	}

	/// <summary>
	/// Checks if the visualization prefab is null and initializes the grid visualizations actor.
	/// </summary>
	/// <param name="visualizePrefab">The prefab to use for visualization.</param>
	/// <param name="gridVisualizations">The actor for grid visualizations.</param>
	/// <returns>True if the prefab is valid, otherwise false.</returns>
	private static bool VisualNullChecker(Prefab visualizePrefab, out Actor gridVisualizations)
	{
		gridVisualizations = null;
		if (visualizePrefab == null)
		{
			Debug.LogWarning($"Prefab is null. Please assign a prefab to visualize the grid.");
			return false;
		}

		// Find or create the parent actor for visualizations
		if (Level.FindActor(VISUAL_PARENT_NAME) == null)
		{
			gridVisualizations = new EmptyActor();
			gridVisualizations.Name = VISUAL_PARENT_NAME;
			if (Level.SpawnActor(gridVisualizations))
			{
				Debug.LogWarning($"Failed to spawn {VISUAL_PARENT_NAME} actor. Do not run this method on Awake");
				return false;
			}
		}

		return true;
	}
}