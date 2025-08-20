using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DataTables.GeneratorCore;

internal static class TagFilterUtils
{
	private static readonly Regex TokenRegex = new Regex(@"[A-Za-z0-9_]+|\(|\)|&&|\|\||!|AND|OR|NOT", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static bool Evaluate(string columnTagText, string filterExpression)
	{
		if (string.IsNullOrWhiteSpace(filterExpression)) return true;
		var tagSet = ParseTagSet(columnTagText);
		return EvaluateExpression(tagSet, filterExpression);
	}

	private static HashSet<string> ParseTagSet(string columnTagText)
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		if (string.IsNullOrWhiteSpace(columnTagText)) return set;
		
		var text = columnTagText.Trim();
		
		// 检查是否包含分隔符（新格式）
		if (Regex.IsMatch(text, @"[^A-Za-z0-9_]+"))
		{
			// 新格式：使用分隔符分割（支持空格、逗号、分号、竖线、中文逗号等）
			var parts = Regex.Split(text, @"[^A-Za-z0-9_]+");
			foreach (var p in parts)
			{
				var t = p.Trim();
				if (t.Length > 0) set.Add(t);
			}
		}
		else
		{
			// 兼容性格式：单个连续字符串，可能是多个单字符标签连接（如"CS"表示"C"和"S"）
			// 先尝试作为整体标签
			set.Add(text);
			// 同时支持单字符拆分（向后兼容）
			foreach (char c in text)
			{
				if (char.IsLetterOrDigit(c))
				{
					set.Add(c.ToString());
				}
			}
		}
		return set;
	}

	private static bool EvaluateExpression(HashSet<string> tagSet, string expr)
	{
		// Shunting-yard 转后缀表达式
		var output = new List<string>();
		var ops = new Stack<string>();
		foreach (Match m in TokenRegex.Matches(expr))
		{
			var tok = NormalizeToken(m.Value);
			if (tok.Length == 0) continue;
			if (IsIdentifier(tok))
			{
				output.Add(tok);
			}
			else if (tok == "(")
			{
				ops.Push(tok);
			}
			else if (tok == ")")
			{
				while (ops.Count > 0 && ops.Peek() != "(") output.Add(ops.Pop());
				if (ops.Count == 0) throw new FormatException("Unmatched parenthesis in tag filter expression");
				ops.Pop();
			}
			else if (IsOperator(tok))
			{
				while (ops.Count > 0 && IsOperator(ops.Peek()) &&
						(Precedence(ops.Peek()) > Precedence(tok) ||
						(Precedence(ops.Peek()) == Precedence(tok) && IsLeftAssociative(tok))))
				{
					output.Add(ops.Pop());
				}
				ops.Push(tok);
			}
		}
		while (ops.Count > 0)
		{
			var op = ops.Pop();
			if (op == "(" || op == ")") throw new FormatException("Unmatched parenthesis in tag filter expression");
			output.Add(op);
		}

		// 计算 RPN
		var stack = new Stack<bool>();
		foreach (var t in output)
		{
			if (IsIdentifier(t))
			{
				stack.Push(tagSet.Contains(t));
			}
			else if (t == "NOT")
			{
				if (stack.Count < 1) throw new FormatException("Invalid NOT operand");
				stack.Push(!stack.Pop());
			}
			else if (t == "AND" || t == "OR")
			{
				if (stack.Count < 2) throw new FormatException("Invalid boolean expression");
				bool b = stack.Pop();
				bool a = stack.Pop();
				stack.Push(t == "AND" ? (a && b) : (a || b));
			}
			else
			{
				throw new FormatException("Unknown token in tag filter expression");
			}
		}
		if (stack.Count != 1) throw new FormatException("Invalid tag filter expression");
		return stack.Pop();
	}

	private static string NormalizeToken(string raw)
	{
		var t = raw.Trim();
		if (t.Length == 0) return string.Empty;
		if (t.Equals("&&", StringComparison.Ordinal)) return "AND";
		if (t.Equals("||", StringComparison.Ordinal)) return "OR";
		if (t.Equals("!", StringComparison.Ordinal)) return "NOT";
		if (t.Equals("AND", StringComparison.OrdinalIgnoreCase)) return "AND";
		if (t.Equals("OR", StringComparison.OrdinalIgnoreCase)) return "OR";
		if (t.Equals("NOT", StringComparison.OrdinalIgnoreCase)) return "NOT";
		if (t == "(" || t == ")") return t;
		return t.ToUpperInvariant();
	}

	private static bool IsIdentifier(string t)
	{
		return !(t == "AND" || t == "OR" || t == "NOT" || t == "(" || t == ")");
	}

	private static bool IsOperator(string t) => t == "AND" || t == "OR" || t == "NOT";

	private static int Precedence(string op) => op == "NOT" ? 3 : op == "AND" ? 2 : 1;

	private static bool IsLeftAssociative(string op) => op != "NOT";
}

