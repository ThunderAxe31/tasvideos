﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TASVideos.ForumEngine
{
	public class BbParser
	{
		private static readonly Regex OpeningTag = new (@"\G
			# Tag name
			(
				[^\p{C}\[\]=\/]+
			)
			# Optional attribute value
			([= ]
				(
					(
						(?'open'\[)
							| [^\p{C}\[\]]
							| (?'close-open'\])
					)+
				)
				(?(open)(?!))
			)?
			# Closing `]`
			\]
		", RegexOptions.IgnorePatternWhitespace);
		private static readonly Regex ClosingTag = new (@"\G
			# Slash before tag name
			\/
			# Tag name
			(
				[^\p{C}\[\]=\/]+
			)
			# Closing `]`
			\]
		", RegexOptions.IgnorePatternWhitespace);
		private static readonly Regex Url = new (@"\G
			https?:\/\/
			(
				[A-Za-z0-9\-._~!$&'()*+,;=:@\/]
				|
				%[A-Fa-f0-9]{2}
			)+
			(
				\?
				(
					[A-Za-z0-9\-._~!$&'()*+,;=:@\/]
					|
					%[A-Fa-f0-9]{2}
				)+
			)?
			(
				\#
				(
					[A-Za-z0-9\-._~!$&'()*+,;=:@\/]
					|
					%[A-Fa-f0-9]{2}
				)+
			)?
		", RegexOptions.IgnorePatternWhitespace);

		// The old system does support attributes in html tags, but only a few that we probably don't want,
		// and it doesn't even support the full html syntax for them.  So forget attributes for now
		private static readonly Regex HtmlOpening = new (@"\G\s*([a-zA-Z]+)\s*>");
		private static readonly Regex HtmlClosing = new (@"\G\s*\/\s*([a-zA-Z]+)\s*>");

		private static readonly Regex HtmlVoid = new (@"\G\s*([a-zA-Z]+)\s*\/?\s*>");

		private class TagInfo
		{
			public enum ChildrenAllowed
			{
				/// <summary>
				/// This tag can have children.
				/// </summary>
				Yes,

				/// <summary>
				/// This tag cannot have children and potential children should be parsed as raw text.
				/// </summary>
				No,

				/// <summary>
				/// If this tag has a non-empty parameter, behaves like Yes, otherwise, like No.
				/// </summary>
				IfParam,
			}

			public ChildrenAllowed Children;

			public enum SelfNestingAllowed
			{
				/// <summary>
				/// This tag can nest in itself freely.
				/// </summary>
				Yes,

				/// <summary>
				/// This tag cannot nest in itself, and should autoclose any existing instances of itself at any level.
				/// </summary>
				No,

				/// <summary>
				/// This tag can nest in itself, but it can't be an immediate child of itself.
				/// </summary>
				NoImmediate,
			}

			public SelfNestingAllowed SelfNesting;
		}

		private static readonly Dictionary<string, TagInfo> KnownTags = new ()
		{
			// basic text formatting, no params, and body is content
			{ "b", new () },
			{ "i", new () },
			{ "u", new () },
			{ "s", new () },
			{ "sub", new () },
			{ "sup", new () },
			{ "tt", new () },
			{ "left", new () },
			{ "right", new () },
			{ "center", new () },
			{ "spoiler", new () },
			{ "warning", new () },
			{ "note", new () },
			{ "highlight", new () },

			// with optional params
			{ "quote", new () }, // optional author
			{ "code", new () { Children = TagInfo.ChildrenAllowed.No } }, // optional language
			{ "img", new () { Children = TagInfo.ChildrenAllowed.No } }, // optional size
			{ "url", new () { Children = TagInfo.ChildrenAllowed.IfParam, SelfNesting = TagInfo.SelfNestingAllowed.No } }, // optional url.  if not given, url in body
			{ "email", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like url
			{ "video", new () { Children = TagInfo.ChildrenAllowed.No } }, // like img
			{ "google", new () { Children = TagInfo.ChildrenAllowed.No } }, // search query in body.  optional param `images`
			{ "thread", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like url, but the link is a number
			{ "post", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like thread
			{ "movie", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like thread
			{ "submission", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like thread
			{ "userfile", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like thread
			{ "wip", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like thread (in fact, identical to userfile except for text output)
			{ "wiki", new () { Children = TagInfo.ChildrenAllowed.IfParam } }, // like thread, but the link is a page name

			// other stuff
			{ "frames", new () { Children = TagInfo.ChildrenAllowed.No } }, // no params.  body is something like `200` or `200@60.1`
			{ "color", new () }, // param is a css (?) color
			{ "bgcolor", new () }, // like color
			{ "size", new () }, // param is something relating to font size TODO: what are the values?
			{ "noparse", new () { Children = TagInfo.ChildrenAllowed.No } },

			// list related stuff
			{ "list", new () }, // OLs have a param with value ??
			{ "*", new () { SelfNesting = TagInfo.SelfNestingAllowed.NoImmediate } },

			// tables
			{ "table", new () { SelfNesting = TagInfo.SelfNestingAllowed.No } },
			{ "tr", new () { SelfNesting = TagInfo.SelfNestingAllowed.No } },
			{ "td", new () { SelfNesting = TagInfo.SelfNestingAllowed.No } }
		};

		private static readonly HashSet<string> KnownNonEmptyHtmlTags = new ()
		{
			// html parsing, except the empty tags <br> and <hr>, as they immediately close
			// so their parse state is not needed
			"b",
			"i",
			"em",
			"u",
			"pre",
			"code",
			"tt",
			"strike",
			"s",
			"del",
			"sup",
			"sub",
			"div",
			"small"
		};

		public static Element Parse(string text, bool allowHtml, bool allowBb)
		{
			var p = new BbParser(text, allowHtml, allowBb);
			p.ParseLoop();
			return p._root;
		}

		public static bool ContainsHtml(string text, bool allowBb)
		{
			var p = new BbParser(text, true, allowBb);
			p.ParseLoop();
			return p._didHtml;
		}

		private readonly Element _root = new () { Name = "_root" };
		private readonly Stack<Element> _stack = new ();

		private readonly string _input;
		private int _index;

		private readonly bool _allowHtml;
		private readonly bool _allowBb;
		private bool _didHtml;

		private readonly StringBuilder _currentText = new ();

		private BbParser(string input, bool allowHtml, bool allowBb)
		{
			_input = input;
			_allowHtml = allowHtml;
			_allowBb = allowBb;
			_stack.Push(_root);
		}

		private void FlushText()
		{
			if (_currentText.Length > 0)
			{
				_stack.Peek().Children.Add(new Text { Content = _currentText.ToString() });
				_currentText.Clear();
			}
		}

		private void Push(Element e)
		{
			_stack.Peek().Children.Add(e);
			_stack.Push(e);
		}

		private bool ChildrenExpected()
		{
			if (KnownTags.TryGetValue(_stack.Peek().Name, out var state))
			{
				return state.Children switch
				{
					TagInfo.ChildrenAllowed.No => false,
					TagInfo.ChildrenAllowed.IfParam => _stack.Peek().Options != "",
					TagInfo.ChildrenAllowed.Yes => true,
				};
			}

			// "li" or "_root" or any of the html tags
			return true;
		}

		private void ParseLoop()
		{
			while (_index < _input.Length)
			{
				{
					Match m;
					if (_allowBb
						&& ChildrenExpected()
						&& (m = Url.Match(_input, _index)).Success
						&& !_stack.Any(element => element.Name == "url")
					)
					{
						FlushText();
						Push(new Element { Name = "url" });
						_currentText.Append(m.Value);
						FlushText();
						_index += m.Length;
						_stack.Pop();
						continue;
					}
				}

				var c = _input[_index++];
				if (_allowBb && c == '[') // check for possible tags
				{
					Match m;
					if (ChildrenExpected() && (m = OpeningTag.Match(_input, _index)).Success)
					{
						var name = m.Groups[1].Value;
						var options = m.Groups[3].Value;
						if (options.Length >= 2 && options[0] == '"' && options[^1] == '"')
						{
								options = options[1..^1];
						}

						if (KnownTags.TryGetValue(name, out var state))
						{
							var e = new Element { Name = name, Options = options };
							FlushText();
							_index += m.Length;
							if (state.SelfNesting == TagInfo.SelfNestingAllowed.No)
							{
								// try to pop a matching tag
								foreach (var node in _stack)
								{
									if (node.Name == name)
									{
										while (true)
										{
											if (_stack.Pop().Name == name)
											{
												break;
											}
										}

										break;
									}
								}
							}
							else if (state.SelfNesting == TagInfo.SelfNestingAllowed.NoImmediate)
							{
								// try to pop a matching tag but only at this level
								if (_stack.Peek().Name == name)
								{
									_stack.Pop();
								}
							}

							Push(e);
							continue;
						}
						else
						{
							// Tag not recognized?  OK, process as raw text
						}
					}
					else if ((m = ClosingTag.Match(_input, _index)).Success)
					{
						var name = m.Groups[1].Value;
						var matching = _stack.FirstOrDefault(elt => elt.Name == name);
						if (matching != null)
						{
							FlushText();
							_index += m.Length;
							while (true)
							{
								if (_stack.Pop() == matching)
								{
									break;
								}
							}

							continue;
						}
						else
						{
							// closing didn't match opening?  OK, process as raw text
						}
					}
					else
					{
						// '[' but not followed by a valid tag?  OK, process as raw text
					}
				}
				else if (_allowHtml && c == '<') // check for possible HTML tags
				{
					Match m;
					if ((m = HtmlClosing.Match(_input, _index)).Success)
					{
						var name = m.Groups[1].Value.ToLowerInvariant();
						name = "html:" + name;
						var topName = _stack.Peek().Name;
						if (name == topName)
						{
							FlushText();
							_index += m.Length;
							_stack.Pop();
							_didHtml = true;
							continue;
						}
						else
						{
							// closing didn't match opening?  OK, process as raw text
						}
					}
					else if (ChildrenExpected())
					{
						if ((m = HtmlOpening.Match(_input, _index)).Success)
						{
							var name = m.Groups[1].Value.ToLowerInvariant();
							if (KnownNonEmptyHtmlTags.Contains(name))
							{
								var e = new Element { Name = "html:" + name };
								FlushText();
								_index += m.Length;
								Push(e);
								_didHtml = true;
								continue;
							}
							else
							{
								// tag not recognized?  Might be a void tag, or raw text
							}
						}

						if ((m = HtmlVoid.Match(_input, _index)).Success)
						{
							var name = m.Groups[1].Value.ToLowerInvariant();
							if (name == "br" || name == "hr")
							{
								var e = new Element { Name = "html:" + name };
								FlushText();
								_index += m.Length;
								Push(e);
								_stack.Pop();
								_didHtml = true;
								continue;
							}
							else
							{
								// tag not recognized?  OK, process as raw text
							}
						}
					}
					else
					{
						// '<' but not followed by a valid tag?  OK, process as raw text
					}
				}

				_currentText.Append(c);
			}

			FlushText();
		}
	}
}
