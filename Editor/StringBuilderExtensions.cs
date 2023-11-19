using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace dev.jellejurre.controllercodeconverter.Editor
{

	public static class StringBuilderExtensions
	{
		public static string GetParameterDefault<T>(string parameterName, T value, T defaultValue, string valueString = null)
		{
			if (valueString == null && value is bool)
			{
				valueString = value.ToString().ToLower();
			}

			if (valueString == null && value is float)
			{
				valueString = value.ToString() + "f";
			}
			
			if (!EqualityComparer<T>.Default.Equals(value, defaultValue))
			{
				return $"{parameterName}: {valueString ?? value.ToString()}";
			}

			return "";
		}

		public static string GetVector3Constructor(Vector3 v)
		{
			return $"new Vector3({GetFloat(v.x)}, {GetFloat(v.y)}, {GetFloat(v.z)})";
		}
		
		public static string GetVector2Constructor(Vector2 v)
		{
			return $"new Vector2({GetFloat(v.x)}, {GetFloat(v.y)})";
		}

		public static string GetFloat(float f)
		{
			if (float.IsPositiveInfinity(f))
			{
				return "float.PositiveInfinity";
			}

			if (float.IsNegativeInfinity(f))
			{
				return "float.NegativeInfinity";
			}

			return f + "f";
		}

		public static string GetArrayConstructor(string[] values, int tabs, string typename = "")
		{
			StringBuilder builder = new StringBuilder().AppendLine().AppendLine($"new {typename}[] {{", tabs);
			for (var i = 0; i < values.Length; i++)
			{
				builder.Append(values[i], tabs + 1).AppendCommaIfNotLast(i, values.Length).AppendLine();
			}
			builder.Append("}", tabs);
			return builder.ToString();
		}
		
		public static string SanitizeForVariableName(string s)
		{
			if (s == null)
			{
				return "";
			}
			
			if (s == "")
			{
				return "Empty";
			}

			if (char.IsDigit(s[0]))
			{
				s = "v" + s;
			}
			
			return new string(s.Where(char.IsLetterOrDigit).ToArray());
		}

		public static string GetMethodCall(string functionName, string[] parameters, int tabs)
		{
			return new StringBuilder().AppendMethodCall(functionName, parameters, tabs).ToString();
		}
		
		public static StringBuilder AppendMethodCall(this StringBuilder builder, string functionName, string[] parameters, int tabs)
		{
			builder.AppendTabs(tabs).Append($"{functionName}(");
			parameters = parameters.Where(x => x != "").ToArray();
			for (var i = 0; i < parameters.Length; i++)
			{
				builder.Append(parameters[i]);
				builder.AppendCommaIfNotLast(i, parameters.Length);
			}
			builder.Append(")");
			return builder;
		}

		public static StringBuilder AppendIfNotLast(this StringBuilder builder, int index, int length, string value)
		{
			return builder.Append((index == length - 1) ? "" : value);
		}

		
		public static StringBuilder AppendCommaIfNotLast(this StringBuilder builder, int index, int length) => builder.AppendIfNotLast(index, length, ", ");

		public static StringBuilder AppendLineIfNotLast(this StringBuilder builder, int index, int length, string s = "") => builder.Append(s).AppendIfNotLast(index, length, Environment.NewLine);


		public static StringBuilder AppendTabs(this StringBuilder builder, int tabs)
		{
			return builder.Append(new string('\t', tabs));
		}

		public static StringBuilder Append(this StringBuilder builder, string text, int tabs)
		{
			return builder.AppendTabs(tabs).Append(text);
		}
		
		public static StringBuilder AppendLine(this StringBuilder builder, string text, int tabs)
		{
			return builder.AppendTabs(tabs).Append(text).AppendLine();
		}
	}
}