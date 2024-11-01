using System;
using FlaxEngine;

namespace GridSystem
{
	/// <summary>
	/// 
	/// </summary>
	public class GSystem : GamePlugin
	{
		/// <inheritdoc />
		public GSystem()
		{
			_description = new PluginDescription
			{
				Name = "GridSystem",
				Category = "System",
				Author = "alcoranpaul",
				AuthorUrl = "https://github.com/alcoranpaul",
				HomepageUrl = "https://github.com/alcoranpaul",
				RepositoryUrl = "https://github.com/FlaxEngine/GridSystem",
				Description = "2-D grid system for Flax Engine",
				Version = new Version(1, 0),
				IsAlpha = false,
				IsBeta = false,
			};
		}

		/// <inheritdoc />
		public override void Initialize()
		{
			base.Initialize();
		}

		/// <inheritdoc />
		public override void Deinitialize()
		{
			// Use it to cleanup data

			base.Deinitialize();
		}
	}
}
