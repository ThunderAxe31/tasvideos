using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using TASVideos.WikiEngine.AST;

namespace TASVideos.WikiEngine
{
	public class NewParser
	{
		public class SyntaxException : Exception
		{
			public SyntaxException(string msg) : base(msg) { }
		}

		private List<INode> _output = new List<INode>();
		private List<INodeWithChildren> _stack = new List<INodeWithChildren>();
		private StringBuilder _currentText = new StringBuilder();
		private string _input;
		private int _index = 0;
		private delegate void ActionType(NewParser p);
		private bool _parsingInline = false;

		private void Abort(string msg)
		{
			throw new SyntaxException("msg"); 
		}
		private bool Eat(char c)
		{
			if (EOF() || c != _input[_index])
				return false;
			_index++;
			return true;
		}
		private bool Eat(string s)
		{
			int j;
			for (j = 0; j < s.Length; j++)
			{
				if (_index + j >= _input.Length || s[j] != _input[_index + j])
					return false;
			}
			_index += j;
			return true;
		}
		private char Eat()
		{
			return _input[_index++];
		}
		private bool EatEOL()
		{
			return Eat("\r\n") || Eat('\r') || Eat('\n');
		}
		private bool EOF()
		{
			return _index == _input.Length;
		}
		private string EatToBracket()
		{
			var ret = new StringBuilder();
			for (var i = 1; i > 0;)
			{
				if (EOF())
					Abort("Unexpected EOF parsing text in []");
				if (EatEOL())
					Abort("Unexpected EOL parsing text in []");
				var c = Eat();
				if (c == '[')
					i++;
				else if (c == ']')
					i--;
				if (i > 0)
					ret.Append(c);
			}
			return ret.ToString();
		}
		private string EatClassText()
		{
			var ret = new StringBuilder();
			if (!EOF())
				Eat(' '); // OK if this fails?
			while (!EOF() && !EatEOL())
				ret.Append(Eat());
			return ret.ToString();
		}
		private string EatTabName()
		{
			var ret = new StringBuilder();
			while (!EOF() && !EatEOL() && !Eat('%'))
				ret.Append(Eat());
			DiscardLine();
			return ret.ToString();
		}
		private string EatSrcEmbedText()
		{
			var ret = new StringBuilder();
			while (!EOF() && !Eat("%%END_EMBED"))
				ret.Append(Eat());
			DiscardLine();
			return ret.ToString();
		}
		private void DiscardLine()
		{
			while (!EOF() && !EatEOL()) { }
		}
		private void AddText(char c)
		{
			_currentText.Append(c);
		}
		private void AddText(string s)
		{
			_currentText.Append(s);
		}
		private void FinishText()
		{
			if (_currentText.Length > 0)
			{
				var t = new Text(_currentText.ToString());
				_currentText.Clear();
				if (_stack.Count > 0)
					_stack[_stack.Count - 1].Children.Add(t);
				else
					_output.Add(t);
			}
		}
		private bool TryPop(string tag)
		{
			for (var i = _stack.Count - 1; i >= 0; i--)
			{
				var e = _stack[i];
				if (e.Type == NodeType.Element && ((Element)e).Tag == tag)
				{
					FinishText();
					_stack.RemoveRange(i, _stack.Count - i);
					return true;
				}
			}
			return false;
		}
		private void Pop(string tag)
		{
			if (!TryPop(tag))
				throw new InvalidOperationException("Internal parser error: Pop");
		}
		private void PopOrPush(string tag)
		{
			if (!TryPop(tag))
				Push(tag);
		}
		private void Push(INodeWithChildren n)
		{
			AddNonChild(n);
			_stack.Add(n);
		}
		private void Push(string tag)
		{
			Push(new Element(tag));
		}
		private bool TryPopIf()
		{
			for (var i = _stack.Count - 1; i >= 0; i--)
			{
				var e = _stack[i];
				if (e.Type == NodeType.IfModule)
				{
					FinishText();
					_stack.RemoveRange(i, _stack.Count - i);
					return true;
				}
			}
			return false;
		}
		private void AddNonChild(INode n)
		{
			FinishText();
			if (_stack.Count > 0)
				_stack[_stack.Count - 1].Children.Add(n);
			else
				_output.Add(n);
		}
		private void ClearBlockTags()
		{
			// any block level tag that isn't explicitly closed in markup
			// except ul / ol, which have special handling
			if (!TryPop("h1")
				&& !TryPop("h2")
				&& !TryPop("h3")
				&& !TryPop("h4")
				&& !TryPop("table")
				&& !TryPop("dl")
				&& !TryPop("blockquote")
				&& !TryPop("pre")
				&& !TryPop("p"))
			{ }
		}
		private bool In(string tag)
		{
			foreach (var e in _stack)
			{
				if (e.Type == NodeType.Element && ((Element)e).Tag == tag)
					return true;
			}
			return false;
		}
		private void SwitchToInline()
		{
			if (_parsingInline)
				throw new InvalidOperationException("Internal parser error");
			_parsingInline = true;
		}
		private void SwitchToLine()
		{
			if (!_parsingInline)
				throw new InvalidOperationException("Internal parser error");
			_parsingInline = false;
		}

		private void ParseInlineText()
		{
			if (Eat("__"))
				PopOrPush("b");
			else if (Eat("''"))
				PopOrPush("em");
			else if (Eat("---"))
				PopOrPush("del");
			else if (Eat("(("))
				Push("small");
			else if (Eat("))"))
			{
				if (!TryPop("small"))
					AddText("))");
			}
			else if (Eat("{{"))
				Push("tt");
			else if (Eat("}}"))
			{
				if (!TryPop("tt"))
					AddText("}}");
			}
			else if (Eat("««"))
				Push("q");
			else if (Eat("»»"))
			{
				if (!TryPop("q"))
					AddText("»»");
			}
			else if (Eat("%%%"))
			{
				AddNonChild(new Element("br"));
			}
			else if (Eat("[["))
				AddText('[');
			else if (Eat("]]"))
				AddText(']');
			else if (Eat("[if:"))
			{
				Push(new IfModule(EatToBracket()));
			}
			else if (Eat("[endif]"))
			{
				if (!TryPopIf())
					Abort("[endif] missing corresponding [if:]");
			}
			else if (Eat('['))
			{
				AddNonChild(new Module(EatToBracket()));
			}
			else if (In("dt") && Eat(':'))
			{
				Pop("dt");
				Push("dd");
			}
			else if (In("th") && Eat("||"))
			{
				Pop("th");
				if (EatEOL())
					SwitchToLine();
				else
					Push("th");
			}
			else if (In("td") && Eat('|'))
			{
				Pop("td");
				if (EatEOL())
					SwitchToLine();
				else
					Push("td");
			}
			else if (EatEOL())
			{
				AddText('\n');
				SwitchToLine();
			}
			else
				AddText(Eat());
		}

		private void ParseStartLine()
		{
			if (Eat("%%QUOTE"))
			{
				var author = EatClassText();
				ClearBlockTags();
				var e = new Element("quote");
				if (author != "")
					e.Attributes["data-author"] = author;
				Push(e);
			}
			else if (Eat("%%QUOTE_END"))
			{
				if (!TryPop("quote"))
					Abort("Mismatched %%QUOTE_END");
			}
			else if (Eat("%%DIV"))
			{
				var className = EatClassText();
				ClearBlockTags();
				var e = new Element("div");
				if (className != "")
					e.Attributes["class"] = className;
				Push(e);		
			}
			else if (Eat("%%DIV_END"))
			{
				if (!TryPop("div"))
					Abort("Mismatched %%DIV_END");
			}
			else if (Eat("%%TAB "))
			{
				var name = EatTabName();
				ClearBlockTags();
				if (!In("vtabs") && !In("htabs"))
					Push("vtabs");
				else
					TryPop("tabs");
				var e = new Element("tab");
				e.Attributes["data-name"] = name;
				Push(e);
			}
			else if (Eat("%%TAB_START%%"))
			{
				DiscardLine();
				ClearBlockTags();
				Push("vtabs");
			}
			else if (Eat("%%HTAB_START%%"))
			{
				DiscardLine();
				ClearBlockTags();
				Push("htabs");
			}
			else if (Eat("[if:"))
			{
				ClearBlockTags();
				Push(new IfModule(EatToBracket()));
			}
			else if (Eat("[endif]"))
			{
				if (!TryPopIf())
					Abort("[endif] missing corresponding [if:]");
			}
			else if (Eat("----"))
			{
				while(Eat('-')) { }
				ClearBlockTags();
				AddNonChild(new Element("hr"));
			}
			else if (Eat("!!!!"))
			{
				ClearBlockTags();
				Push("h1");
				SwitchToInline();
			}
			else if (Eat("!!!"))
			{
				ClearBlockTags();
				Push("h2");
				SwitchToInline();
			}
			else if (Eat("!!"))
			{
				ClearBlockTags();
				Push("h3");
				SwitchToInline();
			}
			else if (Eat("!"))
			{
				ClearBlockTags();
				Push("h4");
				SwitchToInline();
			}
			else if (Eat("%%TOC%%"))
			{
				DiscardLine();
				ClearBlockTags();
				AddNonChild(new Element("toc"));
			}
			else if (Eat('*') || Eat('#'))
			{
				throw new NotImplementedException();
			}
			else if (Eat("||"))
			{				
				if (In("tbody"))
					Pop("table");
				if (!In("table"))
				{
					ClearBlockTags();
					Push("table");
				}
				if (!In("thead"))
					Push("thead");
				Push("th");
				SwitchToInline();
			}
			else if (Eat('|'))
			{
				if (In("thead"))
					Pop("thead");
				if (!In("table"))
				{
					ClearBlockTags();
					Push("table");
				}
				if (!In("tbody"))
					Push("tbody");
				Push("tr");
				SwitchToInline();
			}
			else if (Eat(';'))
			{
				if (!In("dl"))
				{
					ClearBlockTags();
					Push("dl");
				}
				Push("dt");
			}
			else if (Eat("%%SRC_EMBED"))
			{
				var lang = EatClassText();
				ClearBlockTags();
				var e = new Element("code");
				if (lang != "")
					e.Attributes["data-syntax"] = lang;
				e.Children.Add(new Text(EatSrcEmbedText()));
				AddNonChild(e);
			}
			else if (Eat('>'))
			{
				if (!In("blockquote"))
				{
					ClearBlockTags();
					Push("blockquote");
				}
				SwitchToInline();
			}
			else if (Eat(' '))
			{
				if (!In("pre"))
				{
					ClearBlockTags();
					Push("pre");
				}
				SwitchToInline();
			}
			else if (!EatEOL())
			{
				if (!In("p"))
				{
					ClearBlockTags();
					Push("p");
				}
				SwitchToInline();
			}
		}

		private void ParseLoop()
		{
			while (!EOF())
			{
				if (_parsingInline)
					ParseInlineText();
				else
					ParseStartLine();
			}
			FinishText();
		}

		public static List<INode> Parse(string content)
		{
			var p = new NewParser { _input = content };
			p.ParseLoop();
			return p._output;
		}
	}
}
