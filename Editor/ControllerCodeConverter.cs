using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace dev.jellejurre.controllercodeconverter.Editor
{
	public class ControllerCodeConverter : EditorWindow
	{
		public AnimatorController controller;
		public string controllerName;
		public string outputPath;

		[MenuItem("Tools/Jellejurre/ControllerCodeConverter/Creator")]
		public static void ShowWindow()
		{
			ControllerCodeConverter wnd = GetWindow<ControllerCodeConverter>();
			wnd.titleContent = new GUIContent("Controller Code Converter");
		}

		private void OnGUI()
		{
			GUILayout.Label("A Tool to convert your Animator Controller to a C# Script");
			controller = (AnimatorController)EditorGUILayout.ObjectField("Controller To Convert", controller,
				typeof(AnimatorController), false);
			controllerName = EditorGUILayout.TextField("Controller Name", controllerName);
			using (new GUILayout.HorizontalScope())
			{
				GUILayout.Label("Output Path: " + outputPath);
				if (GUILayout.Button("Set new output path"))
				{
					string selectedPath = "Assets" + EditorUtility.OpenFolderPanel("Select Directory", "Assets/", "")
						.Replace(Application.dataPath, "");
					AssetDatabase.ImportAsset(selectedPath);
					if (AssetDatabase.IsValidFolder(selectedPath))
					{
						outputPath = selectedPath;
					}
				}
			}

			if (!AssetDatabase.IsValidFolder(outputPath))
			{
				GUILayout.Label("Please select an output path.");
				return;
			}

			if (string.IsNullOrEmpty(controllerName))
			{
				GUILayout.Label("Please enter a Controller Name");
				return;
			}

			if (GUILayout.Button("Convert To Code"))
			{
				ControllerCodeGenerator.GenerateControllerCode(outputPath, controllerName, controller);
			}
		}
	}
}
