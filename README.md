<div align="center">

# Controller Code Converter

[![Generic badge](https://img.shields.io/github/downloads/jellejurre/Controller-Code-Converter/total?label=Downloads)](https://github.com/jellejurre/Controller-Code-Converter/releases/latest)
[![Generic badge](https://img.shields.io/badge/License-MIT-informational.svg)](https://github.com/VRLabs/Local-Mirror-Detection/blob/main/LICENSE)
[![Generic badge](https://img.shields.io/badge/Unity-2019.4.31f1-lightblue.svg)](https://unity3d.com/unity/whats-new/2019.4.31)
[![Generic badge](https://img.shields.io/badge/SDK-AvatarSDK3-lightblue.svg)](https://vrchat.com/home/download)

Convert your Controller into a script that generates it!

### ⬇️ [Download Latest Version](https://github.com/jellejurre/Controller-Code-Converter/releases/latest)

</div>

## Features
Controller Code Converter is a tool to convert an Animator Controller into a script that will generate that controller.

This is useful for when you want to automate setting up a controller, like when making a setup script that requires specific setup, but you don't want to write the hundreds of lines of code by hand.

It has support for everything that VRChat uses, which includes SDK 3.0 State Behaviours, Sub State Machines, Avatar Masks, and BlendTrees.

# Usage
- Import the latest unity package from the [Releases Page](https://github.com/jellejurre/Controller-Code-Converter/releases/latest)
- Click Tools/Jellejurre/ControllerCodeConverter/Creator
- Fill in the shown fields and select an output folder
- Press the `Convert To Code` button

This will generate two files in your output folder under a `/Code` subfolder. One of these is a helper file called "ControllerGenerationMethods" and one is your code file.

You can run the code generation by clicking `Tools/jellejurre/ControllerCodeConverter/Create/[Your Controller Name]` and it will put the controller in your target folder.

# Sample Output
After using Controller Code Converter on the sample VRChat IK Pose controller, the following output is generated:

(Note that the contents of the Proxy animation aren't filled in because it is a Proxy animation. Other animations will have their contents filled)
```csharp
#if UNITY_EDITOR
using System;
using UnityEditor;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using System.Linq;
using static SampleOutput.ControllerGenerationMethods;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;
using static VRC.SDKBase.VRC_AnimatorTrackingControl;
using static VRC.SDKBase.VRC_AvatarParameterDriver;

namespace SampleOutput
{
	public class SampleOutput
	{
		public static Dictionary<string, object> objectCache;	
	
		private const string controllerName = "SampleOutput";
		private const string controllerPath = "Assets/out";
		[MenuItem("Tools/Jellejurre/ControllerCodeConverter/Create/" + controllerName)]
		public static void CreateController()
		{
			string animationsPath = controllerPath + "/Animations";
			string maskPath =  controllerPath + "/Masks";
			bool succeeded = false;
			try {
				
				AssetDatabase.StartAssetEditing();
				
				foreach (string path in new[] { controllerPath, animationsPath, maskPath })
				{
					if (!System.IO.Directory.Exists(path + "/"))
					{
						System.IO.Directory.CreateDirectory(path);
						AssetDatabase.ImportAsset(path);
					}
				}
			
				objectCache = new Dictionary<string, object>();
				AnimatorControllerLayer[] layers = new AnimatorControllerLayer[]
				{
					GenerateLayerUtilityLayer()
				};
	
				AnimatorControllerParameter[] parameters = new AnimatorControllerParameter[]
				{
	
				};
	
				AnimatorController controller = GenerateController(controllerName, layers, parameters);
				AssetDatabase.CreateAsset(controller, $"{controllerPath}/{controllerName}.controller");
				SerializeController(controller);
				
				List<AnimatorTransition> transitions = GenerateStateMachineTransitions();
				foreach (var transition in transitions){
					Add(controller, transition);
				}
							
				foreach (var clip in controller.animationClips)
				{
					if (!AssetDatabase.IsMainAsset(clip)){
						var uniqueFileName = AssetDatabase.GenerateUniqueAssetPath($"{controllerPath}/Animations/{clip.name}.anim");
						AssetDatabase.CreateAsset(clip, uniqueFileName);
					}
				}
				
				foreach (var layer in controller.layers)
				{
					if (layer.avatarMask != null)
					{
						AssetDatabase.CreateAsset(layer.avatarMask, $"{controllerPath}/Masks/{layer.avatarMask.name}.mask");
					}
				}
				
				AssetDatabase.SaveAssets();
				succeeded = true;
			}
			finally {
				AssetDatabase.StopAssetEditing();
			}
			if(succeeded){
				EditorUtility.DisplayDialog("Succeeded", $"Creating controller succeeded. you can find it at {controllerPath}/{controllerName}.controller", "Ok");
			} else {
				EditorUtility.DisplayDialog("Failed", $"Creating controller failed. please notify @jellejurre on discord about this", "Ok");
			}
		}
		
		public static AnimatorControllerLayer GenerateLayerUtilityLayer(){
			AnimatorStateMachine StateMachineUtilityLayer = GenerateStateMachineUtilityLayer();
			AnimatorControllerLayer LayerUtilityLayer = GenerateLayer("Utility Layer", StateMachineUtilityLayer, defaultWeight: 0f);
			return LayerUtilityLayer;
		}

		public static AnimatorStateMachine GenerateStateMachineUtilityLayer(){
			AnimationClip Clipproxyikpose = GenerateClip("proxy_ikpose");

			AnimatorState StateIKPose = GenerateState("IK Pose", motion: Clipproxyikpose);

			objectCache["StateIKPose"] = StateIKPose;

			ChildAnimatorState[] states = new ChildAnimatorState[] {
				GenerateChildState(new Vector3(324f, 108f, 0f), StateIKPose)
			};

			AnimatorStateMachine StateMachineUtilityLayer = GenerateStateMachine("Utility Layer", new Vector3(336f, 0f, 0f), new Vector3(50f, 120f, 0f), new Vector3(588f, 108f, 0f), states: states, defaultState: StateIKPose);
			objectCache["StateMachineUtilityLayer"] = StateMachineUtilityLayer;

			return StateMachineUtilityLayer;
		}

		public static List<AnimatorTransition> GenerateStateMachineTransitions(){
			List<AnimatorTransition> transitions = new List<AnimatorTransition>();


			return transitions;
		}

	}
}
#endif
```