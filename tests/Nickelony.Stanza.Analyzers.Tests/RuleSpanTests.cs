using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nickelony.Stanza.Analyzers.BlankLines;
using Nickelony.Stanza.Tests.Shared;

namespace Nickelony.Stanza.Analyzers.Tests;

/// <summary>Pins the text each rule reports at, and the text of each additional location.</summary>
[TestClass]
public sealed class RuleSpanTests
{
	// An option a row needs beyond the defaults, keyed by rule id.
	private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> s_options = new(
		StringComparer.Ordinal
	)
	{
		[DiagnosticDescriptors.BreakExpressionOffSignatureLine.Id] = new Dictionary<string, string>
		{
			["stanza_max_line_length"] = "60",
		},

		[DiagnosticDescriptors.ShortenLongMember.Id] = new Dictionary<string, string>
		{
			["stanza_max_member_statements"] = "3",
		},

		[DiagnosticDescriptors.NameClosingConditionalDirective.Id] = new Dictionary<string, string>
		{
			["stanza_min_lines_to_name_endif"] = "0",
		},
	};

	// A rule that ships on and owns the gap of a row, keyed by rule ID; the row runs with it off.
	private static readonly Dictionary<string, DiagnosticDescriptor[]> s_ownersOff = new(
		StringComparer.Ordinal
	)
	{
		[DiagnosticDescriptors.SeparateCommentedSibling.Id] =
		[
			DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether,
		],
	};

	// Each location reads as the first line of its text, then its line and column.
	// A plus sign joins an additional location, and a bar separates two diagnostics.
	public static IEnumerable<object[]> Rows { get; } =
	[
		[
			DiagnosticDescriptors.SeparateDeclarationRuns.Id,

			"""
				class C
				{
					void M()
					{
						var first = 1;
						var second = 2;
						int third = 3;
						return;
					}
				}
				""",

			"int@7:3+var second = 2;@6:3",
		],

		[
			DiagnosticDescriptors.SeparateInvocationRuns.Id,

			"""
				class C
				{
					void M()
					{
						var value = 1;
						DoThing();
						DoThing();
						return;
					}

					void DoThing()
					{
					}
				}
				""",

			"DoThing@6:3+var value = 1;@5:3|return@8:3+DoThing();@7:3",
		],

		[
			DiagnosticDescriptors.SeparateAssertionRuns.Id,

			"""
				class C
				{
					void M()
					{
						var value = 1;
						Assert.That(value);
						Assert.That(value);
						return;
					}
				}
				""",

			"Assert@6:3+var value = 1;@5:3|return@8:3+Assert.That(value);@7:3",
		],

		[
			DiagnosticDescriptors.SeparateAssignmentRuns.Id,

			"""
				class C
				{
					void M(int first, int second)
					{
						var value = 1;
						first = value;
						second = value;
						return;
					}
				}
				""",

			"first@6:3+var value = 1;@5:3|return@8:3+second = value;@7:3",
		],

		[
			DiagnosticDescriptors.SeparateEventSubscriptionRuns.Id,

			"""
				class C
				{
					event System.Action Changed;
					event System.Action Closed;

					void Handler()
					{
					}

					void M()
					{
						var value = 1;
						Changed += Handler;
						Closed += Handler;
						return;
					}
				}
				""",

			"Changed@13:3+var value = 1;@12:3|return@15:3+Closed += Handler;@14:3",
		],

		[
			DiagnosticDescriptors.SeparateAwaitRuns.Id,

			"""
				class C
				{
					async System.Threading.Tasks.Task M()
					{
						var value = 1;
						await Load(value);
						_ = await Save();
						return;
					}
				}
				""",

			"await@6:3+var value = 1;@5:3|return@8:3+_ = await Save();@7:3",
		],

		[
			DiagnosticDescriptors.SeparateMemberGroups.Id,

			"""
				class C
				{
					public int Count => 1;
					public int Total => 2;
					public void Reset() => Call(1, 2);
				}
				""",

			"public@5:2+public int Total => 2;@4:2",
		],

		[
			DiagnosticDescriptors.SeparateModifierGroups.Id,

			"""
				class C
				{
					static int First() => 1;
					static int Second() => 2;
					int Third() => 3;
				}
				""",

			"int@5:2+static int Second() => 2;@4:2",
		],

		[
			DiagnosticDescriptors.SeparateSameTypeDeclarationRuns.Id,

			"""
				class C
				{
					void M()
					{
						int a = 1;
						int b = 2;
						string c = "x";
						string d = "y";
					}
				}
				""",

			"string@7:3+int b = 2;@6:3",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineConstructs.Id,

			"""
				class C
				{
					void M()
					{
						if (true)
						{
							Call(1, 2);
						}
						Call(1, 2);
					}
				}
				""",

			"Call@9:3+if (true)@5:3",
		],

		[
			DiagnosticDescriptors.KeepTwoUnitsOfContainerTogether.Id,

			"""
				private static string Describe(object data)
				{
					string result = ResultGiver.GetResult(data);

					return result;
				}
				""",

			"return@5:2+string result = ResultGiver.GetResult(data);@3:2",
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseStatements.Id,

			"""
				void M()
				{
					a = 1;

					b = 2;

					c = 3;

					d = 4;
				}
				""",

			"b@5:2+a = 1;@3:2|c@7:2+b = 2;@5:2|d@9:2+c = 3;@7:2",
		],

		[
			DiagnosticDescriptors.GatherRunOfLooseMembers.Id,

			"""
				class C
				{
					int a;

					int b;

					int c;

					int d;
				}
				""",

			"int@5:2+int a;@3:2|int@7:2+int b;@5:2|int@9:2+int c;@7:2",
		],

		[
			DiagnosticDescriptors.KeepRunOfStatementsTogether.Id,

			"""
				void M()
				{
					int first = 1;

					int second = 2;
				}
				""",

			"int@5:2+int first = 1;@3:2",
		],

		[
			DiagnosticDescriptors.KeepRunOfMembersTogether.Id,

			"""
				class C
				{
					int a;

					int b;
				}
				""",

			"int@5:2+int a;@3:2",
		],

		[
			DiagnosticDescriptors.SeparateCommentedSibling.Id,

			"""
				var item = new Item
				{
					Name = "a",
					// Count = 2,
					Count = 3,
				};
				""",

			"Count@5:2+Name = \"a\"@3:2",
		],

		[
			DiagnosticDescriptors.SeparateJumpStatement.Id,

			"""
				void M()
				{
					a = 1;
					b = 2;
					return;
				}
				""",

			"return@5:2+b = 2;@4:2",
		],

		[
			DiagnosticDescriptors.SeparateBaseCall.Id,

			"""
				class C : Stage
				{
					public override void OnStart()
					{
						base.OnStart();
						a = 1;
						b = 2;
						base.OnStop();
					}
				}
				""",

			"a@6:3+base.OnStart();@5:3|base@8:3+b = 2;@7:3",
		],

		[
			DiagnosticDescriptors.SeparateGuardClauses.Id,

			"""
				void M(int x)
				{
					if (x == 0) return;
					a = 1;
					b = 2;
				}
				""",

			"a@4:2+if (x == 0) return;@3:2",
		],

		[
			DiagnosticDescriptors.CollapseExtraBlankLines.Id,

			"""
				class C
				{
					private int _total;



					private int _count;
				}
				""",

			"@4:1",
		],

		[
			DiagnosticDescriptors.SeparateImportSection.Id,

			"""
				using System;
				using System.Linq;
				class C
				{
				}
				""",

			"class@3:1+using System.Linq;@2:1",
		],

		[
			DiagnosticDescriptors.SeparateImportDirectiveGroups.Id,

			"""
				global using System;
				global using System.Linq;
				using System.Numerics;
				using System.Text;
				""",

			"using@3:1+global using System.Linq;@2:1",
		],

		[
			DiagnosticDescriptors.SeparateFileScopedNamespaceDeclarationFromFirstMember.Id,

			"""
				namespace N;
				class C
				{
					int Value => 1;
				}
				""",

			"class@2:1+N@1:11",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineAccessors.Id,

			"""
				public int Value
				{
					get
					{
						return _value;
					}
					set
					{
						_value = value;
					}
				}
				""",

			"set@7:2+get@3:2",
		],

		[
			DiagnosticDescriptors.KeepMultiLineAccessorsTogether.Id,

			"""
				public int Value
				{
					get
					{
						return _value;
					}

					set
					{
						_value = value;
					}
				}
				""",

			"set@8:2+get@3:2",
		],

		[
			DiagnosticDescriptors.KeepSingleLineAccessorsTogether.Id,

			"""
				public int Value
				{
					get { return _value; }

					set { _value = value; }
				}
				""",

			"set@5:2+get { return _value; }@3:2",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineSwitchSections.Id,

			"""
				class C
				{
					void M(int value)
					{
						switch (value)
						{
							case 1:
								return;
							case 2:
								return;
						}
					}
				}
				""",

			"case@9:4+case 1:@7:4",
		],

		[
			DiagnosticDescriptors.KeepMultiLineSwitchSectionsTogether.Id,

			"""
				switch (state)
				{
					case OrderState.Pending:
						logger.LogInformation("Waiting for the payment.");
						break;

					case OrderState.Shipped:
						logger.LogInformation("On its way.");
						break;
				}
				""",

			"case@7:2+case OrderState.Pending:@3:2",
		],

		[
			DiagnosticDescriptors.KeepSingleLineSwitchSectionsTogether.Id,

			"""
				switch (state)
				{
					case OrderState.Pending: return "waiting";

					case OrderState.Shipped: return "shipped";
				}
				""",

			"case@5:2+case OrderState.Pending: return \"waiting\";@3:2",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineSwitchExpressionArms.Id,

			"""
				class C
				{
					string M(int value)
					{
						return value switch
						{
							1 => Build(
								value
							),
							_ => "two",
						};
					}

					string Build(int value)
					{
						return value.ToString();
					}
				}
				""",

			"_@10:4+1 => Build(@7:4",
		],

		[
			DiagnosticDescriptors.KeepMultiLineSwitchExpressionArmsTogether.Id,

			"""
				string status = state switch
				{
					OrderState.Pending => Describe(
						state),

					OrderState.Shipped => "shipped",
				};
				""",

			"OrderState@6:2+OrderState.Pending => Describe(@3:2",
		],

		[
			DiagnosticDescriptors.KeepSingleLineSwitchExpressionArmsTogether.Id,

			"""
				string status = state switch
				{
					OrderState.Pending => "waiting",

					OrderState.Shipped => "shipped",
				};
				""",

			"OrderState@5:2+OrderState.Pending => \"waiting\"@3:2",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineInitializerMembers.Id,

			"""
				var item = new Item
				{
					Name = "a",
					Tags = new List<string>
					{
						"x",
					},
					Count = 3,
				};
				""",

			"Tags@4:2+Name = \"a\"@3:2|Count@8:2+Tags = new List<string>@4:2",
		],

		[
			DiagnosticDescriptors.KeepMultiLineInitializerMembersTogether.Id,

			"""
				var item = new Item
				{
					Name = "a",

					Tags = new List<string>
					{
						"x",
					},

					Count = 3,
				};
				""",

			"Tags@5:2+Name = \"a\"@3:2|Count@10:2+Tags = new List<string>@5:2",
		],

		[
			DiagnosticDescriptors.KeepSingleLineInitializerMembersTogether.Id,

			"""
				var item = new Item
				{
					Name = "a",

					Count = 3,
				};
				""",

			"Count@5:2+Name = \"a\"@3:2",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineCollectionElements.Id,

			"""
				var handlers = new IHandler[]
				{
					new ConfigurationHandler(
						ConfigurationSectionKind.Application),
					new ValidationHandler(
						ConfigurationSectionKind.Application),
				};
				""",

			"new@5:2+new ConfigurationHandler(@3:2",
		],

		[
			DiagnosticDescriptors.KeepMultiLineCollectionElementsTogether.Id,

			"""
				var handlers = new IHandler[]
				{
					new ConfigurationHandler(
						ConfigurationSectionKind.Application),

					new ValidationHandler(
						ConfigurationSectionKind.Application),
				};
				""",

			"new@6:2+new ConfigurationHandler(@3:2",
		],

		[
			DiagnosticDescriptors.KeepSingleLineCollectionElementsTogether.Id,

			"""
				var kinds = new[]
				{
					ConfigurationSectionKind.Application,

					ConfigurationSectionKind.User,
				};
				""",

			"ConfigurationSectionKind@5:2+ConfigurationSectionKind.Application@3:2",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineArguments.Id,

			"""
				var options = ConfigurationSectionOptions.Create(
					"Application",
					validateOnLoad: true,
					onInvalid: (key, value) =>
					{
						logger.LogWarning("Invalid value for {Key}: {Value}", key, value);
					});
				""",

			"onInvalid@4:2+validateOnLoad: true@3:2",
		],

		[
			DiagnosticDescriptors.KeepMultiLineArgumentsTogether.Id,

			"""
				var options = ConfigurationSectionOptions.Create(
					"Application",

					onInvalid: (key, value) =>
					{
						logger.LogWarning("Invalid value for {Key}: {Value}", key, value);
					});
				""",

			"onInvalid@4:2+\"Application\"@2:2",
		],

		[
			DiagnosticDescriptors.KeepSingleLineArgumentsTogether.Id,

			"""
				var options = ConfigurationSectionOptions.Create(
					"Application",

					validateOnLoad: true);
				""",

			"validateOnLoad@4:2+\"Application\"@2:2",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineParameters.Id,

			"""
				public static void Bind(
					string sectionName,
					IReadOnlyDictionary<
						string,
						ConfigurationSectionOptions> options)
				{
					Configure(sectionName, options);
				}
				""",

			"IReadOnlyDictionary@3:2+string sectionName@2:2",
		],

		[
			DiagnosticDescriptors.KeepMultiLineParametersTogether.Id,

			"""
				public static void Bind(
					string sectionName,

					IReadOnlyDictionary<
						string,
						ConfigurationSectionOptions> options)
				{
					Configure(sectionName, options);
				}
				""",

			"IReadOnlyDictionary@4:2+string sectionName@2:2",
		],

		[
			DiagnosticDescriptors.KeepSingleLineParametersTogether.Id,

			"""
				public void Register(
					int capacity,

					int retries)
				{
					Connect(capacity, retries);
				}
				""",

			"int@4:2+int capacity@2:2",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineDeclarators.Id,

			"""
				string sectionName = options.SectionName
						?? DefaultSectionName,
					displayName = options.DisplayName;
				""",

			"displayName@3:2+sectionName = options.SectionName@1:8",
		],

		[
			DiagnosticDescriptors.KeepMultiLineDeclaratorsTogether.Id,

			"""
				string sectionName = options.SectionName
						?? DefaultSectionName,

					displayName = options.DisplayName;
				""",

			"displayName@4:2+sectionName = options.SectionName@1:8",
		],

		[
			DiagnosticDescriptors.KeepSingleLineDeclaratorsTogether.Id,

			"""
				int first = 1,

					second = 2;
				""",

			"second@3:2+first = 1@1:5",
		],

		[
			DiagnosticDescriptors.SeparateClauseKeywordFromBraceBeforeIt.Id,

			"""
				if (options.ValidateOnLoad)
				{
					Validate(options);
				}
				else
				{
					options.ValidateOnLoad = true;
				}
				""",

			"else@5:1+}@4:1",
		],

		[
			DiagnosticDescriptors.KeepClauseKeywordsAdjacent.Id,

			"""
				class C
				{
					void M(bool flag)
					{
						if (flag)
						{
							Call(1, 2);
						}

						else
						{
							Call(2, 3);
						}
					}
				}
				""",

			"else@10:3+}@8:3",
		],

		[
			DiagnosticDescriptors.RemoveBracePadding.Id,

			"""
				class C
				{
					void M()
					{

						Call(1, 2);

					}
				}
				""",

			"{@4:2+Call@6:3|}@8:2+;@6:13",
		],

		[
			DiagnosticDescriptors.CollapseEmptyBlocks.Id,

			"""
				class C
				{
					void M()
					{
						if (true)
						{
						}
					}
				}
				""",

			"}@7:3+{@6:3",
		],

		[
			DiagnosticDescriptors.UseBracesConsistently.Id,

			"""
				class C
				{
					void M(bool flag, bool other)
					{
						if (flag)
							Call(1, 2);
						else if (other)
						{
							Call(1, 2);
							Call(2, 3);
						}
						else
							Call(3, 4);
					}
				}
				""",

			"Call@6:4+Call(1, 2);@6:4|Call@13:4+Call(3, 4);@13:4",
		],

		[
			DiagnosticDescriptors.RemoveRedundantBraces.Id,

			"""
				if (root is null)
				{
					return;
				}
				else
				{
					Skip();
				}
				""",

			"{@2:1+}@4:1|{@6:1+}@8:1",
		],

		[
			DiagnosticDescriptors.UseBlockBodyForMultiLineMember.Id,

			"""
				private static Kind Pick(bool first) =>
					first ? Kind.One
						: Kind.Two;
				""",

			"=>@1:38+first ? Kind.One@2:2",
		],

		[
			DiagnosticDescriptors.UseExpressionBodyForSingleLineMember.Id,

			"""
				public int GetValue()
				{
					return _value;
				}
				""",

			"{@2:1",
		],

		[
			DiagnosticDescriptors.UseBlockBodyForMultiLineValueMember.Id,

			"""
				public override string Name =>
					First
							+ " "
							+ Last;
				""",

			"=>@1:29+First@2:2",
		],

		[
			DiagnosticDescriptors.UseExpressionBodyForSingleLineValueMember.Id,

			"""
				public int Count
				{
					get { return _items.Count; }
				}
				""",

			"{@3:6",
		],

		[
			DiagnosticDescriptors.SeparateRegionMarkerFromNeighbors.Id,

			"""
				class C
				{
					private int _total;
					#region Fields
					private int _count;
					#endregion
					private int _sum;
				}
				""",

			"	#region Fields@4:1+;@3:20|	private int _count;@5:1+#region Fields@4:2|	#endregion@6:1+;@5:20|	private int _sum;@7:1+#endregion@6:2",
		],

		[
			DiagnosticDescriptors.NameClosingRegionDirective.Id,

			"""
				#region Fields (mutable)
				private int _count;
				#endregion
				""",

			"#endregion@3:1+#region Fields (mutable)@1:1",
		],

		[
			DiagnosticDescriptors.NameClosingConditionalDirective.Id,

			"""
				#if DEBUG
				private int _count;
				#endif
				""",

			"#endif@3:1+#if DEBUG@1:1",
		],

		[
			DiagnosticDescriptors.AddTrailingComma.Id,

			"""
				enum Level
				{
					Low,
					High
				}
				""",

			"High@4:2",
		],

		[
			DiagnosticDescriptors.RemoveTrailingComma.Id,

			"""
				enum Level
				{
					Low,
					High,
				}
				""",

			",@4:6",
		],

		[
			DiagnosticDescriptors.RemoveEmptyRegions.Id,

			"""
				class C
				{
					private int _total;

					#region Fields
					#endregion

					private int _sum;
				}
				""",

			"	#region Fields@5:1",
		],

		[
			DiagnosticDescriptors.SeparateConditionalBlockFromNeighbors.Id,

			"""
				class C
				{
					private int _total;
					#if TRACE
					private int _trace;
					#endif
					private int _sum;
				}
				""",

			"	#if TRACE@4:1+;@3:20|	private int _sum;@7:1+#endif@6:2",
		],

		[
			DiagnosticDescriptors.BreakExpressionOffSignatureLine.Id,

			"""
				private static Kind Pick(bool first) => first ? Kind.One : Kind.Two;
				""",

			"first ? Kind.One : Kind.Two@1:41+=>@1:38",
		],

		[
			DiagnosticDescriptors.KeepArrowOnSignatureLine.Id,

			"""
				private static Kind Pick(bool first)
					=>
					first ? Kind.One : Kind.Two;
				""",

			"=>@2:2+)@1:36",
		],

		[
			DiagnosticDescriptors.PutArrowOnLineAfterSignature.Id,

			"""
				private static Kind Pick(bool first) =>
					first ? Kind.One : Kind.Two;
				""",

			"=>@1:38+)@1:36",
		],

		[
			DiagnosticDescriptors.NameArgumentsOfCall.Id,

			"""
				class C
				{
					static void Draw(int width, int height, bool smooth, string path) { }

					void M()
					{
						Draw(640, 480, true, "image.png");
					}
				}
				""",

			"(640, 480, true, \"image.png\")@7:7",
		],

		[
			DiagnosticDescriptors.NameLiteralArgumentsOfCall.Id,

			"""
				class C
				{
					static void Save(string path, bool overwrite, object backup) { }

					void M(string path)
					{
						Save(path, true, null);
					}
				}
				""",

			"(path, true, null)@7:7",
		],

		[
			DiagnosticDescriptors.RemoveRedundantArgumentName.Id,

			"""
				class C
				{
					static void Draw(int width, int height, bool smooth, string path) { }

					int width;
					int height;
					string path;

					void M()
					{
						Draw(width: width, height: height, smooth: true, path: path);
					}
				}
				""",

			"width:@11:8|height:@11:22|path:@11:52",
		],

		[
			DiagnosticDescriptors.BreakUpLongTernaryChain.Id,

			"""
				class C
				{
					int M(int value) =>
						value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0;
				}
				""",

			"value > 2 ? 3 : value > 1 ? 2 : value > 0 ? 1 : 0@4:3",
		],

		[
			DiagnosticDescriptors.ShortenLongCondition.Id,

			"""
				class C
				{
					bool M(bool a, bool b, bool c) =>
						a && b && c && a && b && c;
				}
				""",

			"a && b && c && a && b && c@4:3",
		],

		[
			DiagnosticDescriptors.FlattenDeeplyNestedCode.Id,

			"""
				class C
				{
					void M(bool a)
					{
						if (a)
						{
							if (a)
							{
								if (a)
								{
									if (a)
									{
									}
								}
							}
						}
					}
				}
				""",

			"if@11:6",
		],

		[
			DiagnosticDescriptors.ShortenLongMember.Id,

			"""
				class C
				{
					void Run()
					{
						F();
						F();
						F();
						F();
					}

					void F() { }
				}
				""",

			"Run@3:7",
		],

		[
			DiagnosticDescriptors.ShortenLongParameterList.Id,

			"""
				class C
				{
					void M(int a, int b, int c, int d, int e, int f, int g, int h) { }
				}
				""",

			"(int a, int b, int c, int d, int e, int f, int g, int h)@3:8",
		],

		[
			DiagnosticDescriptors.ShortenLongLine.Id,
			"class C\n{\n    int " + new string('a', 91) + " = 1;\n}\n",
			"= 1;@3:101",
		],

		[
			DiagnosticDescriptors.BreakUpLongElseIfChain.Id,

			"""
				class C
				{
					void M(int n)
					{
						if (n == 1) { }
						else if (n == 2) { }
						else if (n == 3) { }
						else if (n == 4) { }
						else if (n == 5) { }
					}
				}
				""",

			"if@5:3",
		],

		[
			DiagnosticDescriptors.RemoveElseAfterJump.Id,

			"""
				class C
				{
					void M(bool flag)
					{
						if (flag)
						{
							return;
						}
						else
						{
							DoThing();
						}
					}
				}
				""",

			"else@9:3",
		],

		[
			DiagnosticDescriptors.MergeNestedIfStatements.Id,

			"""
				class C
				{
					void M(bool a, bool b)
					{
						if (a)
						{
							if (b)
							{
								DoThing();
							}
						}
					}
				}
				""",

			"if@5:3",
		],

		[
			DiagnosticDescriptors.UsePatternForNullCheck.Id,

			"""
				class C
				{
					bool M(object value) => null != value;
				}
				""",

			"null != value@3:26",
		],

		[
			DiagnosticDescriptors.UseEqualityOperatorForNullCheck.Id,

			"""
				class C
				{
					bool M(object value) => value is not { };
				}
				""",

			"value is not { }@3:26",
		],

		[
			DiagnosticDescriptors.SeparateMultiLineMarkupElements.Id,

			""""
				const string Project = """
					<Project>
					  <ItemGroup>
					    <PackageReference Include="a" />
					  </ItemGroup>
					  <ItemGroup>
					    <PackageReference Include="b" />
					  </ItemGroup>
					</Project>
					""";
				"""",

			"	  <ItemGroup>@6:1+	  </ItemGroup>@5:1",
		],

		[
			DiagnosticDescriptors.KeepMultiLineMarkupElementsTogether.Id,

			""""
				const string Project = """
					<Project>
					  <ItemGroup>
					    <PackageReference Include="a" />
					  </ItemGroup>

					  <ItemGroup>
					    <PackageReference Include="b" />
					  </ItemGroup>
					</Project>
					""";
				"""",

			"	  <ItemGroup>@7:1+	  </ItemGroup>@5:1",
		],

		[
			DiagnosticDescriptors.KeepSingleLineMarkupElementsTogether.Id,

			""""
				const string Project = """
					<Project>
					  <PackageReference Include="a" />

					  <PackageReference Include="b" />
					</Project>
					""";
				"""",

			"	  <PackageReference Include=\"b\" />@5:1+	  <PackageReference Include=\"a\" />@3:1",
		],

		[
			DiagnosticDescriptors.CollapseExtraBlankLinesBetweenMarkupElements.Id,

			""""
				const string Project = """
					<Project>
					  <ItemGroup>
					    <PackageReference Include="a" />
					  </ItemGroup>



					  <ItemGroup>
					    <PackageReference Include="b" />
					  </ItemGroup>
					</Project>
					""";
				"""",

			"	  <ItemGroup>@9:1+	  </ItemGroup>@5:1",
		],

		[
			DiagnosticDescriptors.SeparateMarkupCommentMarkerFromText.Id,

			""""
				const string Project = """
					<Project>
					  <!--Groups every property of the build.-->
					</Project>
					""";
				"""",

			"<!--@3:4|-->@3:43",
		],

		[
			DiagnosticDescriptors.SeparateCommentedMarkupElement.Id,

			""""
				const string Project = """
					<Project>
					  <PackageReference Include="a" />
					  <!-- The second package. -->
					  <PackageReference Include="b" />
					</Project>
					""";
				"""",

			"	  <!-- The second package. -->@4:1+	  <PackageReference Include=\"a\" />@3:1",
		],

		[
			DiagnosticDescriptors.RemoveMarkupElementPadding.Id,

			""""
				const string Project = """
					<Project>

					  <PackageReference Include="a" />
					</Project>
					""";
				"""",

			"	  <PackageReference Include=\"a\" />@4:1+	<Project>@2:1",
		],

		[
			DiagnosticDescriptors.SeparateSlashOfSelfClosingMarkupElement.Id,

			""""
				const string Project = """
					<Project>
					  <PackageReference Include="a"/>
					</Project>
					""";
				"""",

			"/>@3:33",
		],

		[
			DiagnosticDescriptors.KeepSlashOfSelfClosingMarkupElementTight.Id,

			""""
				const string Project = """
					<Project>
					  <PackageReference Include="a" />
					</Project>
					""";
				"""",

			"/>@3:34",
		],

		[
			DiagnosticDescriptors.WriteEmptyMarkupElementAsSelfClosingTag.Id,

			""""
				const string Project = """
					<Project>
					  <PackageReference Include="a"></PackageReference>
					</Project>
					""";
				"""",

			"></PackageReference>@3:33",
		],

		[
			DiagnosticDescriptors.LayOutDocumentationComment.Id,

			"""
				/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>
				/// <returns>The bound options.</returns>
				internal int Bind()
				""",

			"internal@3:1+/// <summary>Binds the options from the configuration section, validates them and lets the host fail at startup rather than at the first request.</summary>@1:1",
		],

		[
			DiagnosticDescriptors.MarkDocumentationParagraphs.Id,

			"""
				/// <remarks>
				/// Binds the options from the configuration section.
				///
				/// Validates them and reports every problem it found.
				/// </remarks>
				""",

			"///@3:1+/// <remarks>@1:1",
		],

		[
			DiagnosticDescriptors.RemoveDocumentationPadding.Id,

			"""
				/// <summary>
				/// Binds the options from the configuration section.
				/// </summary>
				///
				/// <returns>
				/// The bound options.
				/// </returns>
				""",

			"///@4:1+/// <summary>@1:1",
		],

		[
			DiagnosticDescriptors.SeparateSingleLineCommentMarkerFromText.Id,

			"""
				//Binds the options from the configuration section of the host.
				""",

			"//@1:1+//Binds the options from the configuration section of the host.@1:1",
		],

		[
			DiagnosticDescriptors.SeparateDelimitedCommentMarkerFromText.Id,

			"""
				/*Binds the options from the configuration section.*/
				""",

			"/*@1:1+/*Binds the options from the configuration section.*/@1:1|*/@1:52+/*Binds the options from the configuration section.*/@1:1",
		],

		[
			DiagnosticDescriptors.SeparateTrailingCommentFromCode.Id,

			"""
				ConfigurationSectionOptions options = Bind();// the options of the host
				""",

			"// the options of the host@1:46+// the options of the host@1:46",
		],

		[
			DiagnosticDescriptors.CapitalizeDocumentationText.Id,

			"""
				/// <summary>binds the options from the configuration section.</summary>
				""",

			"b@1:14+/// <summary>binds the options from the configuration section.</summary>@1:1",
		],

		[
			DiagnosticDescriptors.EndDocumentationTextWithPeriod.Id,

			"""
				/// <summary>Binds the options from the configuration section</summary>
				""",

			"n@1:61+/// <summary>Binds the options from the configuration section</summary>@1:1",
		],

		[
			DiagnosticDescriptors.MarkLanguageKeywords.Id,

			"""
				/// <summary>Returns null when the section is missing.</summary>
				""",

			"null@1:22+/// <summary>Returns null when the section is missing.</summary>@1:1",
		],

		[
			DiagnosticDescriptors.CollapseWhitespaceInsideTag.Id,

			"""
				/// <summary>Reads <see  langword="null"/> when it is set</summary>
				""",

			"  @1:24+/// <summary>Reads <see  langword=\"null\"/> when it is set</summary>@1:1",
		],

		[
			DiagnosticDescriptors.SeparateSlashOfSelfClosingTag.Id,

			"""
				/// <returns><see langword="null"/> when the section is missing.</returns>
				""",

			"/>@1:34+/// <returns><see langword=\"null\"/> when the section is missing.</returns>@1:1",
		],

		[
			DiagnosticDescriptors.KeepSlashOfSelfClosingTagTight.Id,

			"""
				/// <returns><see langword="null" /> when the section is missing.</returns>
				""",

			"/>@1:35+/// <returns><see langword=\"null\" /> when the section is missing.</returns>@1:1",
		],

		[
			DiagnosticDescriptors.WriteEmptyElementAsSelfClosingTag.Id,

			"""
				/// <summary>Reads the <c></c> options and a break <br></br> after them.</summary>
				/// <remarks></remarks>
				""",

			"<c></c>@1:24+/// <summary>Reads the <c></c> options and a break <br></br> after them.</summary>@1:1|<br></br>@1:52+/// <summary>Reads the <c></c> options and a break <br></br> after them.</summary>@1:1|<remarks></remarks>@2:5+/// <summary>Reads the <c></c> options and a break <br></br> after them.</summary>@1:1",
		],

		[
			DiagnosticDescriptors.LayOutItemsOfDocumentationList.Id,

			"""
				/// <list type="bullet">
				/// <item><description>Reads the section, binds every option and validates the whole of it at startup.</description></item>
				/// </list>
				""",

			"<list type=\"bullet\">@1:5+/// <list type=\"bullet\">@1:1",
		],

		[
			DiagnosticDescriptors.OrderItemsOfDocumentationList.Id,

			"""
				/// <list type="table">
				/// <item>
				/// <term>One</term>
				/// <description>The first.</description>
				/// </item>
				/// <listheader>
				/// <description>Value</description>
				/// <term>Name</term>
				/// </listheader>
				/// </list>
				""",

			"<item>@2:5+/// <list type=\"table\">@1:1",
		],

		[
			DiagnosticDescriptors.OrderElementsOfDocumentationComment.Id,

			"""
				/// <remarks>Reads the value twice.</remarks>
				/// <summary>Binds the value.</summary>
				/// <param name="value">The value to bind.</param>
				internal int Bind(int value)
				""",

			"<remarks>@1:5+/// <remarks>Reads the value twice.</remarks>@1:1",
		],

		[
			DiagnosticDescriptors.KeepDocumentationCommentWithDeclaration.Id,

			"""
				/// <summary>Binds the options from the configuration section.</summary>

				internal int Bind()
				""",

			"internal@3:1+/// <summary>Binds the options from the configuration section.</summary>@1:1",
		],
	];

	[TestMethod]
	[DynamicData(
		nameof(Rows),
		DynamicDataDisplayName = nameof(RowNames.GetRowName),
		DynamicDataDisplayNameDeclaringType = typeof(RowNames)
	)]
	public async Task EachRuleReportsAtTheText(string ruleId, string source, string expected)
	{
		DiagnosticAnalyzer analyzer = RoslynTestHarness.AllAnalyzers.Single(candidate =>
			candidate.SupportedDiagnostics.Single().Id == ruleId
		);

		// A row is a fragment, as a rule page example is, so it need not compile.
		AnalyzerRunResult result = await RoslynTestHarness.ReportAsync(
			source,

			new RunOptions([analyzer])
			{
				AnalyzerConfigOptions = s_options.GetValueOrDefault(ruleId),

				DiagnosticOptions = TestRuleSets.OnSuppressingOthersOf(
					[analyzer.SupportedDiagnostics.Single()],
					s_ownersOff.GetValueOrDefault(ruleId, [])
				),

				RequireCompiles = false,
			}
		);

		// An editor draws no squiggle for an empty span.
		foreach (Diagnostic diagnostic in result.Diagnostics)
		{
			Assert.IsFalse(
				diagnostic.Location.SourceSpan.IsEmpty,
				$"{ruleId} reports an empty span at {diagnostic.Location.GetLineSpan().StartLinePosition}."
			);
		}

		string actual = string.Join(
			"|",

			result
				.Diagnostics.OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
				.Select(diagnostic =>
					Describe(diagnostic.Location)
					+ string.Concat(
						diagnostic.AdditionalLocations.Select(location => "+" + Describe(location))
					)
				)
		);

		Assert.AreEqual(expected, actual);
	}

	[TestMethod]
	public void EveryRuleHasARow()
	{
		string[] missing =
		[
			.. RoslynTestHarness
				.AllAnalyzers.Select(analyzer => analyzer.SupportedDiagnostics.Single().Id)
				.Except(Rows.Select(row => (string)row[0]))
				.Order(StringComparer.Ordinal),
		];

		Assert.IsEmpty(missing, $"No row pins where {string.Join(", ", missing)} reports.");
	}

	[TestMethod]
	[CrossCutting]
	public async Task TheUnitAfterTheGapIsTheWholeMemberAndNotItsFirstTokenNode()
	{
		const string source = """
			class C
			{
				int a;
				int b;
				void M()
				{
				}
			}
			""";

		AnalyzerRunResult result =
			await RoslynTestHarness.ReportAsync<SeparateMultiLineConstructsAnalyzer>(source);

		Diagnostic diagnostic = result.Diagnostics.Single();

		StanzaDiagnosticLookup.NeighborUnits? units = StanzaDiagnosticLookup.FindNeighborUnits(
			diagnostic.Location.SourceTree!.GetRoot(),
			diagnostic
		);

		Assert.IsInstanceOfType<FieldDeclarationSyntax>(units!.Value.Previous);
		Assert.IsInstanceOfType<MethodDeclarationSyntax>(units.Value.Next);
	}

	private static string Describe(Location location)
	{
		string text = location.SourceTree!.GetText().ToString(location.SourceSpan);
		LinePosition start = location.GetLineSpan().StartLinePosition;

		return $"{text.Split('\n')[0].TrimEnd('\r')}@{start.Line + 1}:{start.Character + 1}";
	}
}
