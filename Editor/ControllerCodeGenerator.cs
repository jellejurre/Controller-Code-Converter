using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using static dev.jellejurre.controllercodeconverter.Editor.StringBuilderExtensions;

namespace dev.jellejurre.controllercodeconverter.Editor
{
	public class ControllerCodeGenerator
	{
		public static NameCache nameCache;
		public static List<AnimatorStateMachine> machines;
		public static void GenerateControllerCode(string outputPath, string controllerName, AnimatorController controller)
		{
			nameCache = new NameCache();
			machines = new List<AnimatorStateMachine>();
			string codePath = outputPath + "/Code";

			foreach (string path in new[] { codePath })
			{
				if (!System.IO.Directory.Exists(path + "/"))
				{
					System.IO.Directory.CreateDirectory(path);
					AssetDatabase.ImportAsset(path);
				}
			}

			TextAsset controllerFile = Resources.Load<TextAsset>("ControllerFile");
			TextAsset controllerGenerationMethods = Resources.Load<TextAsset>("ControllerGenerationMethods");

			// Process Generation Methods
			string controllerGenerationMethodsText =
				controllerGenerationMethods.text.Replace("[NAMESPACE]", controllerName);
			string controllerGenerationMethodsPath = codePath + "/ControllerGenerationMethods.cs";
			System.IO.File.WriteAllText(controllerGenerationMethodsPath, controllerGenerationMethodsText);

			AddNamesToCache(controller);
			
			// Process Controller File
			string controllerFileText = controllerFile.text
				.Replace("[NAMESPACE]", controllerName)
				.Replace("[CONTROLLERNAME]", controllerName)
				.Replace("[CONTROLLERPATH]", outputPath)
				.Replace("//[PARAMETERCODE]", GetParameterCode(controller))
				.Replace("//[LAYERCODE]", GetLayerCode(controller))
				.Replace("//[METHODCODE]", GetAllCodeMethods(controller));
			System.IO.File.WriteAllText(codePath + $"/{controllerName}.cs", controllerFileText);
			string controllerFilePath = codePath + $"/{controllerName}.cs";

			AssetDatabase.ImportAsset(controllerGenerationMethodsPath);
			AssetDatabase.ImportAsset(controllerFilePath);
			EditorUtility.DisplayDialog("Succeeded", $"Creating controller code succeeded. you can find it at {codePath}/{controllerName}.cs\nTo run it, you can click on Tools/jellejurre/ControllerCodeConverter/Create/{controllerName}", "Ok");
		}

		public static void AddNamesToCache(AnimatorController controller)
		{
			foreach (var layer in controller.layers)
			{
				string parentName = nameCache.Add(layer, layer.name, "");
				Stack<(string, AnimatorStateMachine)> stack = new Stack<(string, AnimatorStateMachine)>();
				stack.Push((parentName, layer.stateMachine));
				while (stack.Count != 0)
				{
					(string parent, AnimatorStateMachine machine) = stack.Pop();
					string stateMachineName = nameCache.Add(machine, machine.name, $"Generate{parent}");
					foreach (var childState in machine.states)
					{
						nameCache.Add(childState.state, childState.state.name, $"Generate{stateMachineName}");
					}
					foreach (var childStateMachine in machine.stateMachines)
					{
						stack.Push((stateMachineName, childStateMachine.stateMachine));
					}
				}
			}
		}
		
		public static string GetParameterCode(AnimatorController controller)
		{
			StringBuilder builder = new StringBuilder();
			for (var index = 0; index < controller.parameters.Length; index++)
			{
				var param = controller.parameters[index];
				switch (param.type)
				{
					case AnimatorControllerParameterType.Bool:
						builder.AppendMethodCall("GenerateBoolParameter", new[]
							{
								$"\"{param.name}\"",
								GetParameterDefault("defaultBool", param.defaultBool, false,
									param.defaultBool.ToString().ToLower())
							}, 4)
							.AppendCommaIfNotLast(index, controller.parameters.Length)
							.AppendLineIfNotLast(index, controller.parameters.Length);
						break;
					case AnimatorControllerParameterType.Int:
						builder.AppendMethodCall("GenerateIntParameter", new[]
							{
								$"\"{param.name}\"",
								GetParameterDefault("defaultInt", param.defaultInt, 0)
							}, 4)
							.AppendCommaIfNotLast(index, controller.parameters.Length)
							.AppendLineIfNotLast(index, controller.parameters.Length);
						break;
					case AnimatorControllerParameterType.Float:
						builder.AppendMethodCall("GenerateFloatParameter", new[]
							{
								$"\"{param.name}\"",
								GetParameterDefault("defaultFloat", param.defaultFloat, 0.0f)
							}, 4)
							.AppendCommaIfNotLast(index, controller.parameters.Length)
							.AppendLineIfNotLast(index, controller.parameters.Length);
						break;
					case AnimatorControllerParameterType.Trigger:
						builder.AppendMethodCall("GenerateTriggerParameter", new[]
							{
								$"\"{param.name}\"",
							}, 4)
							.AppendCommaIfNotLast(index, controller.parameters.Length)
							.AppendLineIfNotLast(index, controller.parameters.Length);
						break;
				}
			}
			return builder.ToString();
		}

		public static string GetLayerCode(AnimatorController controller)
		{
			StringBuilder builder = new StringBuilder();
			for (var i = 0; i < controller.layers.Length; i++)
			{
				AnimatorControllerLayer layer = controller.layers[i];
				string layerName = nameCache[layer].Value.Item2;
				builder.Append($"Generate{layerName}()", 4).AppendCommaIfNotLast(i, controller.layers.Length).AppendLineIfNotLast(i, controller.layers.Length);
			}
			return builder.ToString();
		}

		public static string GetAllCodeMethods(AnimatorController controller)
		{
			StringBuilder builder = new StringBuilder();
			for (var i = 0; i < controller.layers.Length; i++)
			{
				AnimatorControllerLayer layer = controller.layers[i];
				
				string layerName = nameCache[layer].Value.Item2;
				builder.AppendLine($"public static AnimatorControllerLayer Generate{layerName}(){{", 2);

				string stateMachineName = nameCache[layer.stateMachine].Value.Item2;
				
				builder.Append($"AnimatorStateMachine {stateMachineName} = ", 3)
					.AppendMethodCall($"Generate{stateMachineName}", new string[0], 0)
					.AppendLine(";");

				string avatarMaskName = "";

				AvatarMask avatarMask = layer.avatarMask;
				if (avatarMask != null)
				{
					avatarMaskName = GenerateMaskCode(avatarMask, layerName, builder);
				}
				
				builder.Append($"AnimatorControllerLayer {layerName} = ", 3)
					.AppendMethodCall("GenerateLayer", new []
					{
						$"\"{layer.name}\"",
						$"{stateMachineName}",
						GetParameterDefault("mask", avatarMask, null, avatarMaskName),
						GetParameterDefault("blendingMode", layer.blendingMode, AnimatorLayerBlendingMode.Override, $"AnimatorLayerBlendingMode.{layer.blendingMode.ToString()}"),
						GetParameterDefault("defaultWeight", layer.defaultWeight, 1.0f),
						GetParameterDefault("syncedLayerAffectsTiming", layer.syncedLayerAffectsTiming, false),
						GetParameterDefault("syncedLayerIndex", layer.syncedLayerIndex, -1)
					}, 0)
					.AppendLine(";");

				builder.AppendLine($"return {layerName};", 3);
				
				builder.AppendLine("}", 2).AppendLine();
			}

			foreach (var layer in controller.layers)
			{
				GenerateStateMachineCode(layer.stateMachine, builder);
			}

			GenerateStateMachineTransitionsCode(builder);
			
			return builder.ToString();
		}

		private static string GenerateMaskCode(AvatarMask avatarMask, string layerName, StringBuilder builder)
		{
			string avatarMaskName;
			avatarMaskName = nameCache.Add(avatarMask, avatarMask.name, $"Generate{layerName}");
			string[] transforms = new string[avatarMask.transformCount];
			string[] bools = new string[avatarMask.transformCount];
			for (int t = 0; t < avatarMask.transformCount; t++)
			{
				transforms[t] = avatarMask.GetTransformPath(t);
				bools[t] = avatarMask.GetTransformActive(t).ToString().ToLower();
			}

			List<AvatarMaskBodyPart> bodyParts = new List<AvatarMaskBodyPart>();
			foreach (var part in Enum.GetValues(typeof(AvatarMaskBodyPart)).Cast<AvatarMaskBodyPart>()
				         .Where(x => x != AvatarMaskBodyPart.LastBodyPart))
			{
				if (avatarMask.GetHumanoidBodyPartActive(part))
				{
					bodyParts.Add(part);
				}
			}

			builder.Append($"AvatarMask {avatarMaskName} = ", 3)
				.AppendMethodCall($"GenerateMask", new[]
				{
					$"\"{avatarMask.name}\"",
					GetArrayConstructor(transforms, 3, "string"),
					GetArrayConstructor(bools, 3, "bool"),
					GetArrayConstructor(bodyParts.Select(x => $"AvatarMaskBodyPart.{x.ToString()}").ToArray(), 3)
				}, 0).AppendLine(";");
			return avatarMaskName;
		}

		private static void GenerateStateMachineCode(AnimatorStateMachine stateMachine, StringBuilder builder)
		{
			string stateMachineName = nameCache[stateMachine].Value.Item2;
			machines.Add(stateMachine);
			builder.AppendLine($"public static AnimatorStateMachine Generate{stateMachineName}(){{", 2);
			HashSet<Motion> generatedMotions = new HashSet<Motion>();
			for (var s = 0; s < stateMachine.states.Length; s++)
			{
				GenerateStateCode(builder, stateMachine.states[s].state, stateMachineName, generatedMotions);
			}

			builder.AppendLine();
			
			for (var s = 0; s < stateMachine.states.Length; s++)
			{
				builder.AppendLine($"objectCache[\"{nameCache[stateMachine.states[s].state].Value.Item2}\"] = {nameCache[stateMachine.states[s].state].Value.Item2};", 3);
			}
			
			builder.AppendLine();
			
			builder.AppendLine($"ChildAnimatorState[] states = new ChildAnimatorState[] {{", 3);
			for (var s = 0; s < stateMachine.states.Length; s++)
			{
				AnimatorState state = stateMachine.states[s].state;

				builder.AppendMethodCall("GenerateChildState", new[]
					{
						GetVector3Constructor(stateMachine.states[s].position),
						nameCache[state].Value.Item2
					}, 4).AppendCommaIfNotLast(s, stateMachine.states.Length).AppendLine();
			}

			builder.AppendLine("};", 3).AppendLine();

			string defaultState = "";
			if (stateMachine.defaultState != null)
			{
				(string method, string name) = nameCache[stateMachine.defaultState].Value;
				defaultState = method == $"Generate{stateMachineName}" ? name : $"(AnimatorStateMachine) objectCache[\"{name}\"]";
			}

			builder.Append($"AnimatorStateMachine {stateMachineName} = ", 3).AppendMethodCall("GenerateStateMachine", new[]
			{
				$"\"{stateMachine.name}\"",
				GetVector3Constructor(stateMachine.anyStatePosition),
				GetVector3Constructor(stateMachine.entryPosition),
				GetVector3Constructor(stateMachine.exitPosition),
				"states: states",
				GetParameterDefault("defaultState", stateMachine.defaultState, null, defaultState),
				GetParameterDefault("parentStateMachinePosition", stateMachine.parentStateMachinePosition,
					new Vector3(800, 20, 0), GetVector3Constructor(stateMachine.parentStateMachinePosition))
			}, 0).AppendLine(";");
			
			builder.AppendLine($"objectCache[\"{stateMachineName}\"] = {stateMachineName};", 3).AppendLine();

			if (stateMachine.stateMachines.Length > 0)
			{
				foreach (var childStateMachine in stateMachine.stateMachines)
				{
					string subStateMachineName = nameCache[childStateMachine.stateMachine].Value.Item2;
					builder.AppendLine($"AnimatorStateMachine {subStateMachineName} = Generate{subStateMachineName}();", 3);
				}

				builder.AppendLine($"{stateMachineName}.stateMachines = new ChildAnimatorStateMachine[] {{", 3);

				for (var i = 0; i < stateMachine.stateMachines.Length; i++)
				{
					var childStateMachine = stateMachine.stateMachines[i];
					string subStateMachineName = nameCache[childStateMachine.stateMachine].Value.Item2;
					builder.AppendMethodCall("GenerateChildStateMachine" ,new [] { GetVector3Constructor(childStateMachine.position), subStateMachineName }, 4).AppendCommaIfNotLast(i, stateMachine.stateMachines.Length)
						.AppendLine();
				}
				
				builder.Append($"}};", 3).AppendLine();
			}
			
			for (var s = 0; s < stateMachine.states.Length; s++)
			{
				AnimatorState state = stateMachine.states[s].state;
				GenerateStateTransitionsCode(builder, state, stateMachineName);
			}
			
			GenerateEntryTransitions(stateMachine, builder, stateMachineName);
			
			GenerateAnyStateTransitions(stateMachine, builder, stateMachineName);
			
			builder.AppendLine($"return {stateMachineName};", 3);

			builder.AppendLine("}", 2).AppendLine();
			
			foreach (var childAnimatorStateMachine in stateMachine.stateMachines)
			{
				GenerateStateMachineCode(childAnimatorStateMachine.stateMachine, builder);
			}
		}

		private static void GenerateAnyStateTransitions(AnimatorStateMachine stateMachine, StringBuilder builder,
			string stateMachineName)
		{
			if (stateMachine.anyStateTransitions.Length > 0)
			{
				builder.AppendLine(
					$"{nameCache[stateMachine].Value.Item2}.anyStateTransitions = new AnimatorStateTransition[] {{",
					3);
				for (var t = 0; t < stateMachine.anyStateTransitions.Length; t++)
				{
					AnimatorStateTransition transition = stateMachine.anyStateTransitions[t];
					string destinationState = "";
					string destinationStateMachine = "";
					if (transition.destinationState != null)
					{
						(string method, string name) = nameCache[transition.destinationState].Value;
						destinationState = method == $"Generate{stateMachineName}" ? name : $"(AnimatorState) objectCache[\"{name}\"]";
					}
					
					if (transition.destinationStateMachine != null)
					{
						(string method, string name) = nameCache[transition.destinationStateMachine].Value;
						destinationStateMachine = method == $"Generate{stateMachineName}" ? name : $"(AnimatorStateMachine) objectCache[\"{name}\"]";
					}

					builder.AppendMethodCall("GenerateTransition", new[]
					{
						$"\"{transition.name}\"",
						GetParameterDefault("canTransitionToSelf", transition.canTransitionToSelf, false),
						GetParameterDefault("conditions", transition.conditions.Length, 0,
							GetArrayConstructor(transition.conditions
								.Select(condition => GetMethodCall("GenerateCondition", new[]
								{
									$"AnimatorConditionMode.{condition.mode}",
									$"\"{condition.parameter}\"",
									GetFloat(condition.threshold)
								}, 0)).ToArray(), 5, "AnimatorCondition")),
						GetParameterDefault("destinationState", transition.destinationState, null, destinationState),
						GetParameterDefault("destinationStateMachine", transition.destinationStateMachine, null, destinationStateMachine),
						GetParameterDefault("duration", transition.duration, 0.0f),
						GetParameterDefault("hasFixedDuration", transition.hasFixedDuration, false),
						GetParameterDefault("exitTime", transition.exitTime, 0.0f),
						GetParameterDefault("hasExitTime", transition.hasExitTime, false),
						GetParameterDefault("solo", transition.solo, false),
						GetParameterDefault("mute", transition.mute, false),
						GetParameterDefault("isExit", transition.isExit, false),
						GetParameterDefault("offset", transition.offset, 0.0f),
						GetParameterDefault("orderedInterruption", transition.orderedInterruption, true),
						GetParameterDefault("interruptionSource", transition.interruptionSource,
							TransitionInterruptionSource.None,
							$"TransitionInterruptionSource.{transition.interruptionSource}"),
					}, 4).AppendCommaIfNotLast(t, stateMachine.anyStateTransitions.Length).AppendLine();
				}

				builder.AppendLine("};", 3).AppendLine();
			}
		}
		
		private static void GenerateEntryTransitions(AnimatorStateMachine stateMachine, StringBuilder builder,
			string stateMachineName)
		{
			if (stateMachine.entryTransitions.Length > 0)
			{
				builder.AppendLine(
					$"{nameCache[stateMachine].Value.Item2}.entryTransitions = new AnimatorTransition[] {{",
					3);
				for (var t = 0; t < stateMachine.entryTransitions.Length; t++)
				{
					AnimatorTransition transition = stateMachine.entryTransitions[t];
					string destinationState = "";
					string destinationStateMachine = "";
					if (transition.destinationState != null)
					{
						(string method, string name) = nameCache[transition.destinationState].Value;
						destinationState = method == $"Generate{stateMachineName}" ? name : $"(AnimatorState) objectCache[\"{name}\"]";
					}

					if (transition.destinationStateMachine != null)
					{
						(string method, string name) = nameCache[transition.destinationStateMachine].Value;
						destinationStateMachine = method == $"Generate{stateMachineName}" ? name : $"(AnimatorStateMachine) objectCache[\"{name}\"]";
					}
					
					builder.AppendMethodCall("GenerateStateMachineTransition", new[]
					{
						$"\"{transition.name}\"",
						GetParameterDefault("conditions", transition.conditions.Length, 0,
							GetArrayConstructor(transition.conditions
								.Select(condition => GetMethodCall("GenerateCondition", new[]
								{
									$"AnimatorConditionMode.{condition.mode}",
									$"\"{condition.parameter}\"",
									GetFloat(condition.threshold),
								}, 0)).ToArray(), 5, "AnimatorCondition")),
						GetParameterDefault("destinationState", transition.destinationState, null, destinationState),
						GetParameterDefault("destinationStateMachine", transition.destinationStateMachine, null, destinationStateMachine),
						GetParameterDefault("solo", transition.solo, false),
						GetParameterDefault("mute", transition.mute, false),
						GetParameterDefault("isExit", transition.isExit, false),
					}, 4).AppendCommaIfNotLast(t, stateMachine.entryTransitions.Length).AppendLine();
				}

				builder.AppendLine("};", 3).AppendLine();
			}
		}
		
		private static void GenerateStateMachineTransitionsCode(StringBuilder builder)
		{
			builder.AppendLine($"public static List<AnimatorTransition> GenerateStateMachineTransitions(){{", 2);

			builder.AppendLine("List<AnimatorTransition> transitions = new List<AnimatorTransition>();", 3).AppendLine();
			Dictionary<AnimatorStateMachine, (AnimatorStateMachine, AnimatorTransition[])> machineToMachines = new Dictionary<AnimatorStateMachine, (AnimatorStateMachine, AnimatorTransition[])>();
			foreach (var machine1 in machines)
			{
				foreach (var machine2 in machines)
				{
					machineToMachines[machine1] = (machine2, machine1.GetStateMachineTransitions(machine2));
					if (machine1 != machine2)
					{
						machineToMachines[machine2] = (machine1, machine2.GetStateMachineTransitions(machine1));	
					}
				}
			}
			
			foreach (var machine1 in machineToMachines.Keys)
			{
				(AnimatorStateMachine machine2, AnimatorTransition[] transitions) = machineToMachines[machine1];
				if (transitions.Length == 0)
				{
					continue;
				}

				builder.AppendLine(
					$"((AnimatorStateMachine) objectCache[\"{nameCache[machine1].Value.Item2}\"]).SetStateMachineTransitions((AnimatorStateMachine) objectCache[\"{nameCache[machine2].Value.Item2}\"], new [] {{",
					3);

				for (var t = 0; t < transitions.Length; t++)
				{
					AnimatorTransition transition = transitions[t];
					string destinationState = "";
					string destinationStateMachine = "";
					if (transition.destinationState != null)
					{
						destinationState = $"(AnimatorState) objectCache[\"{nameCache[transition.destinationState].Value.Item2}\"]";
					}

					if (transition.destinationStateMachine != null)
					{
						destinationStateMachine = $"(AnimatorState) objectCache[\"{nameCache[transition.destinationStateMachine].Value.Item2}\"]";
					}
					
					builder.AppendMethodCall("GenerateStateMachineTransition", new[]
					{
						$"\"{transition.name}\"",
						GetParameterDefault("conditions", transition.conditions.Length, 0,
							GetArrayConstructor(transition.conditions
								.Select(condition => GetMethodCall("GenerateCondition", new[]
								{
									$"AnimatorConditionMode.{condition.mode}",
									$"\"{condition.parameter}\"",
									GetFloat(condition.threshold)
								}, 0)).ToArray(), 5, "AnimatorCondition")),
						GetParameterDefault("destinationState", transition.destinationState, null, destinationState),
						GetParameterDefault("destinationStateMachine", transition.destinationStateMachine, null, destinationStateMachine),
						GetParameterDefault("solo", transition.solo, false),
						GetParameterDefault("mute", transition.mute, false),
						GetParameterDefault("isExit", transition.isExit, false),
					}, 4).AppendCommaIfNotLast(t, transitions.Length).AppendLine();
				}
				
				builder.AppendLine("});", 3);
				
				builder.AppendLine($"transitions = transitions.Concat(((AnimatorStateMachine) objectCache[\"{nameCache[machine1].Value.Item2}\"]).GetStateMachineTransitions((AnimatorStateMachine) objectCache[\"{nameCache[machine2].Value.Item2}\"])).ToList();", 3);
			}

			builder.AppendLine().AppendLine("return transitions;", 3);
			builder.AppendLine("}", 2);
		}

		private static void GenerateStateTransitionsCode(StringBuilder builder, AnimatorState state, string stateMachineName)
		{
			if (state.transitions.Length > 0)
			{
				builder.AppendLine(
					$"{nameCache[state].Value.Item2}.transitions = new AnimatorStateTransition[] {{",
					3);
				for (var t = 0; t < state.transitions.Length; t++)
				{
					AnimatorStateTransition transition = state.transitions[t];
					string destinationState = "";
					string destinationStateMachine = "";
					if (transition.destinationState != null)
					{
						(string method, string name) = nameCache[transition.destinationState].Value;
						destinationState = method == $"Generate{stateMachineName}" ? name : $"(AnimatorState) objectCache[\"{name}\"]";
					}
					
					if (transition.destinationStateMachine != null)
					{
						if (!nameCache[transition.destinationStateMachine].HasValue)
						{
							
						}
						(string method, string name) = nameCache[transition.destinationStateMachine].Value;
						destinationStateMachine = method == $"Generate{stateMachineName}" ? name : $"(AnimatorStateMachine) objectCache[\"{name}\"]";
					}

					builder.AppendMethodCall("GenerateTransition", new[]
					{
						$"\"{transition.name}\"",
						GetParameterDefault("canTransitionToSelf", transition.canTransitionToSelf, false),
						GetParameterDefault("conditions", transition.conditions.Length, 0, 
							GetArrayConstructor(transition.conditions
								.Select(condition => GetMethodCall("GenerateCondition", new[]
								{
									$"AnimatorConditionMode.{condition.mode}",
									$"\"{condition.parameter}\"",
									GetFloat(condition.threshold)
								}, 0)).ToArray(), 5, "AnimatorCondition")),
						GetParameterDefault("destinationState", transition.destinationState, null, destinationState),
						GetParameterDefault("destinationStateMachine", transition.destinationStateMachine, null, destinationStateMachine),
						GetParameterDefault("duration", transition.duration, 0.0f),
						GetParameterDefault("hasFixedDuration", transition.hasFixedDuration, false),
						GetParameterDefault("exitTime", transition.exitTime, 0.0f),
						GetParameterDefault("hasExitTime", transition.hasExitTime, false),
						GetParameterDefault("solo", transition.solo, false),
						GetParameterDefault("mute", transition.mute, false),
						GetParameterDefault("isExit", transition.isExit, false),
						GetParameterDefault("offset", transition.offset, 0.0f),
						GetParameterDefault("orderedInterruption", transition.orderedInterruption, true),
						GetParameterDefault("interruptionSource", transition.interruptionSource,
							TransitionInterruptionSource.None,
							$"TransitionInterruptionSource.{transition.interruptionSource}"),
					}, 4).AppendCommaIfNotLast(t, state.transitions.Length).AppendLine();
				}
				
				builder.AppendLine("};", 3).AppendLine();
			}
		}
		
		private static void GenerateStateCode(StringBuilder builder, AnimatorState state, string stateMachineName, HashSet<Motion> generatedMotions)
		{
			string motionName = "";

			
			if (state.motion != null)
			{
				if (!generatedMotions.Contains(state.motion))
				{
					GenerateMotionCode(builder, state.motion, generatedMotions);
				}
				
				motionName = nameCache[state.motion].Value.Item2; // Can use direct access because this is all one big function :)
			}

			string stateName = nameCache[state].Value.Item2;
			
			builder.Append($"AnimatorState {stateName} = ", 3)
				.AppendMethodCall("GenerateState", new[]
				{
					$"\"{state.name}\"",
					GetParameterDefault("writeDefaultValues", state.writeDefaultValues, false),
					GetParameterDefault("tag", state.tag, ""),
					GetParameterDefault("motion", state.motion, null, motionName),
					GetParameterDefault("cycleOffset", state.cycleOffset, 0.0f),
					GetParameterDefault("cycleOffsetParameter", $"\"{state.cycleOffsetParameter}\"", "\"\""),
					GetParameterDefault("cycleOffsetParameterActive", state.cycleOffsetParameterActive, false),
					GetParameterDefault("mirror", state.mirror, false),
					GetParameterDefault("mirrorParameter", $"\"{state.mirrorParameter}\"", "\"\""),
					GetParameterDefault("mirrorParameterActive", state.mirrorParameterActive, false),
					GetParameterDefault("timeParameter", $"\"{state.timeParameter}\"", "\"\""),
					GetParameterDefault("timeParameterActive", state.timeParameterActive, false),
					GetParameterDefault("speed", state.speed, 1.0f),
					GetParameterDefault("speedParameter", $"\"{state.speedParameter}\"", "\"\""),
					GetParameterDefault("speedParameterActive", state.speedParameterActive, false)
				}, 0).AppendLine(";");

			if (state.behaviours.Length > 0)
			{
				builder.AppendLine().AppendLine($"{stateName}.behaviours = new StateMachineBehaviour[] {{", 3);
				for (var i = 0; i < state.behaviours.Length; i++)
				{
					var behaviour = state.behaviours[i];
					if (behaviour is VRCAvatarParameterDriver driver)
					{
						builder.AppendMethodCall($"GenerateParameterDriver", new[]
						{
							GetArrayConstructor(driver.parameters
								.Select(param => GetMethodCall("GenerateParameter", new[]
								{
									$"VRC_AvatarParameterDriver.ChangeType.{param.type}",
									GetParameterDefault("source", param.source, "", $"\"{param.source}\""),
									GetParameterDefault("name", param.name, "", $"\"{param.name}\""),
									GetParameterDefault("value", param.value, 0, GetFloat(param.value)),
									GetParameterDefault("chance", param.chance, 0, GetFloat(param.chance)),
									GetParameterDefault("convertRange", param.convertRange, false, param.convertRange.ToString().ToLower()),
									GetParameterDefault("destMax", param.destMax, 0, GetFloat(param.destMax)),
									GetParameterDefault("destMin", param.destMin, 0, GetFloat(param.destMin)),
									GetParameterDefault("sourceMin", param.sourceMin, 0, GetFloat(param.sourceMin)),
									GetParameterDefault("sourceMax", param.sourceMax, 0, GetFloat(param.sourceMax)),
									GetParameterDefault("valueMin", param.valueMin, 0, GetFloat(param.valueMin)),
									GetParameterDefault("valueMax", param.valueMax, 0, GetFloat(param.valueMax))
								}, 0)).ToArray(), 5, "Parameter"),
							GetParameterDefault("isEnabled", driver.isEnabled, false, driver.isEnabled.ToString().ToLower()), 
							GetParameterDefault("localOnly", driver.localOnly, false, driver.localOnly.ToString().ToLower()), 
							GetParameterDefault("debugString", driver.debugString, "", $"\"{driver.debugString}\"")
						}, 4);
					}

					if (behaviour is VRCAnimatorLayerControl animatorLayerControl)
					{
						builder.AppendMethodCall($"GenerateAnimatorLayerControl", new[]
						{
							$"VRC_AnimatorLayerControl.BlendableLayer.{animatorLayerControl.playable}",
							GetParameterDefault("layer", animatorLayerControl.layer, 0),
							GetParameterDefault("blendDuration", animatorLayerControl.blendDuration, 0,
								GetFloat(animatorLayerControl.blendDuration)),
							GetParameterDefault("goalWeight", animatorLayerControl.goalWeight, 0,
								GetFloat(animatorLayerControl.goalWeight)),
							GetParameterDefault("debugString", animatorLayerControl.debugString, "", $"\"{animatorLayerControl.debugString}\""),
						}, 4);
					}

					if (behaviour is VRCAnimatorLocomotionControl locomotionControl)
					{
						builder.AppendMethodCall($"GenerateLocomotionControl", new[]
						{
							locomotionControl.disableLocomotion.ToString().ToLower(),
							GetParameterDefault("debugString", locomotionControl.debugString, "", $"\"{locomotionControl.debugString}\""),
						}, 4);					
					}

					if (behaviour is VRCAnimatorTrackingControl trackingControl)
					{
						builder.AppendMethodCall($"GenerateTrackingControl", new[]
						{
							GetParameterDefault("trackingHead", trackingControl.trackingHead, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingHead}"),
							GetParameterDefault("trackingLeftHand", trackingControl.trackingLeftHand, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingLeftHand}"),
							GetParameterDefault("trackingRightHand", trackingControl.trackingRightHand, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingRightHand}"),
							GetParameterDefault("trackingHip", trackingControl.trackingHip, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingHip}"),
							GetParameterDefault("trackingLeftFoot", trackingControl.trackingLeftFoot, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingLeftFoot}"),
							GetParameterDefault("trackingRightFoot", trackingControl.trackingRightFoot, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingRightFoot}"),
							GetParameterDefault("trackingLeftFingers", trackingControl.trackingLeftFingers, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingLeftFingers}"),
							GetParameterDefault("trackingRightFingers", trackingControl.trackingRightFingers, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingRightFingers}"),
							GetParameterDefault("trackingEyes", trackingControl.trackingEyes, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingEyes}"),
							GetParameterDefault("trackingMouth", trackingControl.trackingMouth, VRC_AnimatorTrackingControl.TrackingType.NoChange, $"VRC_AnimatorTrackingControl.TrackingType.{trackingControl.trackingMouth}"),
							GetParameterDefault("debugString", trackingControl.debugString, "", $"\"{trackingControl.debugString}\""),
						}, 4);
					}

					if (behaviour is VRCPlayableLayerControl playableLayerControl)
					{
						builder.AppendMethodCall($"GeneratePlayableLayerControl", new[]
						{
							$"VRC_PlayableLayerControl.BlendableLayer.{playableLayerControl.layer}",
							GetParameterDefault("blendDuration", playableLayerControl.blendDuration, 0, GetFloat(playableLayerControl.blendDuration)),
							GetParameterDefault("goalWeight", playableLayerControl.goalWeight, 0, GetFloat(playableLayerControl.goalWeight)),
							GetParameterDefault("debugString", playableLayerControl.debugString, "", $"\"{playableLayerControl.debugString}\""),
						}, 4);
					}

					if (behaviour is VRCAnimatorTemporaryPoseSpace temporaryPoseSpace)
					{
						builder.AppendMethodCall($"GenerateTemporaryPoseSpace", new[]
						{
							GetParameterDefault("delayTime", temporaryPoseSpace.delayTime, 0, GetFloat(temporaryPoseSpace.delayTime)),
							GetParameterDefault("fixedDelay", temporaryPoseSpace.fixedDelay, false, temporaryPoseSpace.fixedDelay.ToString().ToLower()),
							GetParameterDefault("enterPoseSpace", temporaryPoseSpace.enterPoseSpace, false, temporaryPoseSpace.enterPoseSpace.ToString().ToLower()),
							GetParameterDefault("debugString", temporaryPoseSpace.debugString, "", $"\"{temporaryPoseSpace.debugString}\""),
						}, 4);
					}

					builder.AppendCommaIfNotLast(i, state.behaviours.Length);
					builder.AppendLine();
				}

				builder.AppendLine("};", 3);
			}
		}

		private static void GenerateMotionCode(StringBuilder builder, Motion motion, HashSet<Motion> generatedMotions)
		{
			if (motion is AnimationClip clip)
			{
				string clipName = nameCache.Add(clip, clip.name, "");
				builder.Append($"AnimationClip {clipName} = ", 3)
					.AppendMethodCall("GenerateClip", new[]
					{
						$"\"{clip.name}\"",
						GetParameterDefault("wrapMode", clip.wrapMode, WrapMode.Default, $"WrapMode.{clip.wrapMode}"),
						GetParameterDefault("localBounds", clip.localBounds, new Bounds(Vector3.zero, Vector3.zero), $"new Bounds({GetVector3Constructor(clip.localBounds.center)}, {GetVector3Constructor(clip.localBounds.size)})"),
						GetParameterDefault("frameRate", clip.frameRate, 60f, GetFloat(clip.frameRate)),
						GetParameterDefault("legacy", clip.legacy, false, clip.legacy.ToString().ToLower()),
					}, 0).AppendLine(";");
				
				generatedMotions.Add(clip);

				if (clip.name.StartsWith("proxy_"))
				{
					builder.AppendLine();
					return;
				}
				
				
				List<(EditorCurveBinding, AnimationCurve)> floatCurves = AnimationUtility.GetCurveBindings(clip).Select(x => (x, AnimationUtility.GetEditorCurve(clip, x))).ToList();
				
				foreach (var (editorCurveBinding, animationCurve) in floatCurves)
				{
					builder.AppendMethodCall($"AddCurve", new[]
					{
						clipName,
						$"\"{editorCurveBinding.path}\"",
						$"typeof({editorCurveBinding.type.FullName})",
						$"\"{editorCurveBinding.propertyName}\"",
						new StringBuilder().AppendLine().AppendMethodCall("GenerateCurve", new []
						{
							GetParameterDefault("preWrapMode", animationCurve.preWrapMode, WrapMode.ClampForever, $"WrapMode.{animationCurve.preWrapMode}"),
							GetParameterDefault("postWrapMode", animationCurve.postWrapMode, WrapMode.ClampForever, $"WrapMode.{animationCurve.postWrapMode}"),
							GetParameterDefault("keys", animationCurve.keys, Array.Empty<Keyframe>(), 
								GetArrayConstructor(animationCurve.keys.Select(x =>
								{
									return new StringBuilder().AppendMethodCall("GenerateKeyFrame", new []
									{
										GetParameterDefault("time", x.time, 0, GetFloat(x.time)),
										GetParameterDefault("value", x.value, 0, GetFloat(x.value)),
										GetParameterDefault("inTangent", x.inTangent, 0, GetFloat(x.inTangent)),
										GetParameterDefault("outTangent", x.outTangent, 0, GetFloat(x.outTangent)),
										GetParameterDefault("inWeight", x.inWeight, 0, GetFloat(x.inWeight)),
										GetParameterDefault("outWeight", x.outWeight, 0, GetFloat(x.outWeight)),
									}, 0).ToString();
								}).ToArray(), 5, "Keyframe"))
						}, 4).ToString(),
					}, 3).AppendLine(";");
				}
				
				List<(EditorCurveBinding, ObjectReferenceKeyframe[])> referenceCurves = AnimationUtility.GetObjectReferenceCurveBindings(clip).Select(x => (x, AnimationUtility.GetObjectReferenceCurve(clip, x))).ToList();
				
				foreach (var (editorCurveBinding, animationCurve) in referenceCurves)
				{
					builder.AppendMethodCall($"AddObjectCurve", new[]
					{
						clipName,
						$"\"{editorCurveBinding.path}\"",
						$"typeof({editorCurveBinding.type.FullName})",
						$"\"{editorCurveBinding.propertyName}\"",
						GetArrayConstructor(animationCurve.Select(x
							=> $"GenerateObjectReferenceKeyFrame({GetFloat(x.time)}, AssetDatabase.LoadAssetAtPath<{x.value.GetType().FullName}>(AssetDatabase.GUIDToAssetPath(\"{AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(x.value))}\")))").ToArray(), 5, "ObjectReferenceKeyframe")
					}, 3).AppendLine(";");
				}
				
				if (referenceCurves.Count != 0 || floatCurves.Count != 0)
				{
					builder.AppendLine();
				}
			}

			if (motion is BlendTree tree)
			{
				if (generatedMotions.Contains(tree))
				{
					return;
				}
				
				string treeName = nameCache.Add(tree, tree.name, "");
				builder.Append($"BlendTree {treeName} = ", 3)
					.AppendMethodCall("GenerateBlendTree", new[]
					{
						$"\"{tree.name}\"",
						$"BlendTreeType.{tree.blendType}",
						GetParameterDefault("blendParameter", tree.blendParameter, "", $"\"{tree.blendParameter}\""),
						GetParameterDefault("blendParameterY", tree.blendParameterY, "", $"\"{tree.blendParameterY}\""),
						GetParameterDefault("maxThreshold", tree.maxThreshold, 0, GetFloat(tree.maxThreshold)),
						GetParameterDefault("minThreshold", tree.minThreshold, 0, GetFloat(tree.minThreshold)),
						GetParameterDefault("useAutomaticThresholds", tree.useAutomaticThresholds, true, tree.useAutomaticThresholds.ToString().ToLower())
					}, 0).AppendLine(";").AppendLine();
				generatedMotions.Add(tree);
				
				foreach (var childMotion in tree.children)
				{
					if (!generatedMotions.Contains(childMotion.motion))
					{
						GenerateMotionCode(builder, childMotion.motion, generatedMotions);
					}
				}
				
				builder.AppendLine($"{treeName}.children = new ChildMotion[] {{", 3);
				tree.children.ToList().ForEach(child => {});
				for (var i = 0; i < tree.children.Length; i++)
				{
					ChildMotion child = tree.children[i];
					builder.AppendMethodCall("GenerateChildMotion", new []
					{
						nameCache[child.motion].Value.Item2,
						GetVector2Constructor(child.position),
						GetParameterDefault("threshold", child.threshold, 0, GetFloat(child.threshold)),
						GetParameterDefault("timeScale", child.timeScale, 1, GetFloat(child.timeScale)),
						GetParameterDefault("directBlendParameter", child.directBlendParameter, "", $"\"{child.directBlendParameter}\""),
						GetParameterDefault("cycleOffset", child.cycleOffset, 0, GetFloat(child.cycleOffset)),
						GetParameterDefault("mirror", child.mirror, false, child.mirror.ToString().ToLower())
					}, 4).AppendCommaIfNotLast(i, tree.children.Length).AppendLine();
				}
				builder.Append("};", 3).AppendLine().AppendLine();

			}
		}
	}
}