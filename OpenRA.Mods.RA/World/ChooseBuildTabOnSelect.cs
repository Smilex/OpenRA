#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.RA
{
	class ChooseBuildTabOnSelectInfo : ITraitInfo
	{
		public object Create(ActorInitializer init) { return new ChooseBuildTabOnSelect(init); }
	}

	class ChooseBuildTabOnSelect : INotifySelection
	{
		readonly World world;

		public ChooseBuildTabOnSelect(ActorInitializer init)
		{
			world = init.world;
		}

		public void SelectionChanged()
		{
			// Queue-per-structure
			var perqueue = world.Selection.Actors.FirstOrDefault(
				a => a.IsInWorld && a.World.LocalPlayer == a.Owner && a.HasTrait<ProductionQueue>());

			if (perqueue != null)
			{
				return;
			}

			// Queue-per-player
			var types = world.Selection.Actors.Where(a => a.IsInWorld && (a.World.LocalPlayer == a.Owner))
				.SelectMany(a => a.TraitsImplementing<Production>())
				.SelectMany(t => t.Info.Produces)
				.ToArray();

			if (types.Length == 0)
				return;
		}
	}
}
