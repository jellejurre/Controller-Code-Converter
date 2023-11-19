using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using static dev.jellejurre.controllercodeconverter.Editor.StringBuilderExtensions;

namespace dev.jellejurre.controllercodeconverter.Editor
{
	public class NameCache
	{
		private readonly Dictionary<object, (string, string)> nameCache = new Dictionary<object, (string, string)>();

		public (string, string)? this[object obj]
		{
			get
			{
				if (obj is AnimatorControllerLayer l)
				{
					if (nameCache.Keys.Any(x =>
						    x is AnimatorControllerLayer layer && layer.name == l.name &&
						    layer.stateMachine == l.stateMachine))
					{
						return nameCache[nameCache.Keys.First(x =>
							x is AnimatorControllerLayer layer && layer.name == l.name &&
							layer.stateMachine == l.stateMachine)];
					}
				}

				(string, string) test;
				nameCache.TryGetValue(obj, out test);
				
				return test == default ? ((string, string)?)null: test;
			}
		}

		public string Add(object obj, string name, string method)
		{
			if (this[obj].HasValue)
			{
				return this[obj].Value.Item2;
			}
			
			if (name == "")
			{
				name = "Obj";
			}

			if (obj is AnimatorControllerLayer)
			{
				name = "Layer" + name;
			}

			if (obj is AnimatorStateMachine)
			{
				name = "StateMachine" + name;
			}

			if (obj is AnimatorState)
			{
				name = "State" + name;
			}

			if (obj is AnimationClip)
			{
				name = "Clip" + name;
			}

			if (obj is BlendTree)
			{
				name = "Tree" + name;
			}

			if (obj is AvatarMask)
			{
				name = "Mask" + name;
			}

			string filtered = SanitizeForVariableName(name);
			
			int i = 0;
			if (nameCache.Values.Any(x => x.Item2 == filtered))
			{
				string baseName = filtered;
				while (nameCache.Values.Any(x => x.Item2 == baseName + i))
				{
					i++;
				}
				filtered = baseName + i;
			}
			nameCache.Add(obj, (method, filtered));
			return filtered;
		}			
	}
}