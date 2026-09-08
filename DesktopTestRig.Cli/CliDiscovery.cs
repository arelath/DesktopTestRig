namespace DesktopTestRig.Cli;

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopTestRig.Contracts;

/// <summary>Discovery is built from the parser's command model and never loads user configuration or attaches.</summary>
internal static class CliDiscovery
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Converters = { new CliTreeShapeJsonConverter(), new CliMouseButtonJsonConverter(), new CliImageFormatJsonConverter() },
	};

	public static int WriteHelp(string[] args, TextWriter stdout, TextWriter stderr)
	{
		var root = CliRootCommand.Create();
		var normalized = args.Length == 0 ? new[] { "--help" } : args.Select(a => a == "/?" ? "--help" : a).ToArray();
		var parsed = root.Parse(normalized);
		stdout.WriteLine($"{DesktopTestRig.ProductInfo.Name} CLI");
		var result = parsed.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr });
		var path = PathOf(parsed.CommandResult.Command);
		stdout.WriteLine();
		stdout.WriteLine("Built-in defaults (user config may override these; inspect with config get):");
		stdout.WriteLine(JsonSerializer.Serialize(new { common = new CliDefaults().Common, command = CommandDefaults(path) }, JsonOptions));
		stdout.WriteLine("Examples:");
		foreach (var example in Examples(path)) stdout.WriteLine("  DesktopTestRig.Cli.exe " + example);
		stdout.WriteLine("Machine-readable contract: schema" + (path.Length == 0 ? "" : " --command \"" + path + "\""));
		return result;
	}

	public static int WriteSchema(string[] args, TextWriter stdout, Stopwatch stopwatch)
	{
		var options = new CliCommonOptions { Pretty = CliArgumentReader.HasOption(args, "--pretty"), HideEmpty = false };
		var root = CliRootCommand.Create();
		var parse = root.Parse(args);
		try
		{
			if (parse.Errors.Count > 0)
				throw new AutomationException(AutomationErrorCodes.InvalidArguments, string.Join(" ", parse.Errors.Select(e => e.Message)));
			var requested = CliArgumentReader.GetOption(args, "--command");
			var commands = Descendants(root).ToList();
			if (requested is not null)
			{
				commands = commands.Where(c => PathOf(c) == requested.Trim()).ToList();
				if (commands.Count == 0) throw new AutomationException(AutomationErrorCodes.InvalidArguments, $"Unknown command path '{requested}'.");
			}
			var data = new
			{
				schemaVersion = 1,
				defaultsPolicy = "Built-in defaults only. User config and environment may override them; config get shows configured defaults. Target-bound commands require exactly one of --pid, --process, --window-title, or a configured selector.",
				builtInDefaults = new CliDefaults(),
				commands = commands.Select(c => new
				{
					name = PathOf(c), description = c.Description,
					arguments = c.Arguments.Select(a => new { name = a.Name, description = a.Description, type = TypeName(a.ValueType), minValues = a.Arity.MinimumNumberOfValues, maxValues = a.Arity.MaximumNumberOfValues }),
					options = c.Options.Concat(root.Options.Where(o => o.Recursive)).DistinctBy(o => o.Name).Select(o => new
					{
						name = o.Name, aliases = o.Aliases, description = o.Description, type = TypeName(o.ValueType), required = o.Required,
						minValues = o.Arity.MinimumNumberOfValues, maxValues = o.Arity.MaximumNumberOfValues,
							allowedValues = AllowedValues(o), defaultValue = DefaultValue(PathOf(c), o),
							minimum = o.Name is "--limit" or "--scan-limit" ? (int?)1 : o.Name == "--max-depth" ? -1 : null,
					}),
					examples = Examples(PathOf(c)),
					responseData = ResponseTypes(PathOf(c)).Select(TypeKey),
				}),
				responseEnvelope = DescribeType(typeof(CliResponseEnvelope)),
				responseSchemas = commands.SelectMany(c => ResponseTypes(PathOf(c))).Distinct().ToDictionary(TypeKey, DescribeType),
				notes = new[]
				{
					"JSON stdout contains response envelopes; diagnostics go to stderr. Streams emit multiple envelopes.",
					"Optional empty fields can be omitted. Property dictionaries preserve explicit nulls. Open-ended data uses an unconstrained JSON schema.",
					"find.searchComplete describes capture coverage; resultsTruncated describes the result cap. totalMatchCount is a lower bound if coverage is incomplete.",
					"tree --view semantic is a lossy outline. Expand any target with node --subtree or tree --view raw --root. Unknown effective states are omitted.",
				},
			};
			CliOutput.Write(CliResponseFactory.Success("schema", data, stopwatch), options, stdout);
			return 0;
		}
		catch (AutomationException ex)
		{
			CliOutput.Write(CliResponseFactory.Error("schema", ex.ErrorCode, ex.Message, stopwatch), options, stdout);
			return ExitCodeMapper.Map(ex.ErrorCode);
		}
	}

	private static IEnumerable<Command> Descendants(Command command) => command.Subcommands.SelectMany(c => new[] { c }.Concat(Descendants(c)));
	private static string PathOf(Command command) => command is RootCommand ? "" :
		string.Join(" ", command.Parents.OfType<Command>().Select(PathOf).Where(s => s.Length > 0).Append(command.Name));
	private static object? CommandDefaults(string path) => typeof(CliCommandDefaults).GetProperties()
		.FirstOrDefault(p => p.Name.Equals(path.Split(' ')[0], StringComparison.OrdinalIgnoreCase))?.GetValue(new CliDefaults().Commands);
	private static object? DefaultValue(string path, Option option)
	{
		var key = option.Name.TrimStart('-').Replace("-", "", StringComparison.Ordinal);
		if (key == "view") return new CliDefaults().Commands.Tree.View;
		if (key == "scanlimit") return new CliDefaults().TreeLimit;
		key = key switch { "property" => "PropertyEquals", "output" => "OutputPath", "target" => "TargetId", _ => key };
		var command = CommandDefaults(path);
		var prop = command?.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
		var value = prop is null ? typeof(CliCommonDefaults).GetProperties().FirstOrDefault(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase))?.GetValue(new CliDefaults().Common) : prop.GetValue(command);
		return value is Enum e ? e.ToString().ToLowerInvariant() : value;
	}
	private static string TypeName(Type type) => Nullable.GetUnderlyingType(type) is { } value ? TypeName(value) :
		type == typeof(bool) ? "boolean" : type == typeof(int) || type == typeof(long) ? "integer" : type == typeof(double) ? "number" : "string";
	private static string[] AllowedValues(Option option) => option.Name switch
	{
		"--view" => ["raw", "semantic"], "--shape" => ["flat", "nested"], "--format" => ["json", "text"],
		"--after" => ["none", "target", "tree"], "--image-format" => ["png", "jpeg", "jpg", "bmp", "gif"],
		"--button" => ["left", "right", "middle", "double"],
		"--recording-format" => ["condensed-agent", "condensed-diagnostic", "compact-json", "raw-json"],
		"--event" => ["Click", "MouseDoubleClick", "Checked", "Unchecked", "Expanded", "Collapsed"],
		"--operation" => ["Focus", "AcceptDialog", "CancelDialog", "BringIntoView", "Select", "Expand", "Collapse", "Check", "Uncheck"],
		_ => option.ValueType == typeof(bool) ? ["true", "false"] : option.ValueType.IsEnum ? Enum.GetNames(option.ValueType).Select(s => s.ToLowerInvariant()).ToArray() : [],
	};
	private static string[] Examples(string path) => path switch
	{
		"" => ["processes", "tree --pid 1234 --view semantic", "find --help", "schema --command find"],
		"tree" => ["tree --pid 1234 --view semantic --limit 20000", "tree --pid 1234 --view raw --root <targetId> --shape nested"],
		"find" => ["find --pid 1234 --name SaveButton --limit 10 --scan-limit 20000", "find --pid 1234 --text Ready --require-match"],
		"node" or "props" or "selectors" => [$"{path} --pid 1234 --target <targetId>"],
		"schema" => ["schema --command find --pretty", "schema"],
		"config" or "config get" => ["config get", "config get commands.tree.limit"],
		"config set" => ["config set commands.tree.limit 20000"], "config clear" => ["config clear common.process"], "config reset" => ["config reset --yes"],
		"processes" or "version" => [path],
		"type" => ["type --pid 1234 --name SearchBox --value hello --clear-first --after target"],
		"set" => ["set --pid 1234 --target <targetId> --property Text --value hello"],
		"key" => ["key --pid 1234 --keys Ctrl+A"],
		"raise" => ["raise --pid 1234 --target <targetId> --event Click"],
		"invoke" => ["invoke --pid 1234 --target <targetId> --operation Expand"],
		"wheel" => ["wheel --pid 1234 --target <targetId> --delta -120"],
		"drag" => ["drag --pid 1234 --target <sourceId> --to-target <destinationId>"],
		"click" or "focus" => [$"{path} --pid 1234 --name SaveButton --after target"],
		"wait" => ["wait --pid 1234 --text Ready --timeout-ms 5000"],
		"screenshot" => ["screenshot --pid 1234 --out capture.png"],
		"record" or "record semantic" => ["record semantic --pid 1234 --out recording.dft.txt --duration-ms 5000"],
		"pipe" => ["pipe status --pid 1234"], "stream" => ["stream visual-tree --pid 1234 --duration-ms 1000"],
		_ => [$"{path} --pid 1234"],
	};
	private static Type[] ResponseTypes(string path) => path switch
	{
		"tree" => [typeof(TreeSnapshotData), typeof(SemanticTreeData)], "find" => [typeof(FindResultData)],
		"node" => [typeof(NodeResultData)], "props" => [typeof(PropsResultData)], "selectors" => [typeof(SelectorSuggestionData)],
		"processes" => [typeof(ProcessListData)], "version" => [typeof(ProductVersionData)], "screenshot" => [typeof(ScreenshotResultData)],
		"wait" => [typeof(FindResultData)], "drag" => [typeof(TwoTargetActionExecutionResult)],
		"click" or "wheel" or "focus" or "type" or "key" or "set" or "raise" or "invoke" => [typeof(ActionExecutionResult)],
		"ping" => [typeof(ProtocolCommandData<PingCommandResponse>)], "pipe status" => [typeof(ProtocolCommandData<PipeStatusCommandResponse>)],
		"record semantic" => [typeof(SemanticRecordingFileData)],
		_ when path.StartsWith("stream ", StringComparison.Ordinal) => [typeof(StreamMessage)],
		_ => [typeof(object)],
	};

	// JSON Schema with recursive definitions; no required optional fields because --hide-empty prunes them.
	private static string TypeKey(Type t) => t.IsGenericType ? t.Name.Split('`')[0] + "Of" + string.Join("And", t.GetGenericArguments().Select(TypeKey)) : t.Name;

	private static object DescribeType(Type type)
	{
		var definitions = new Dictionary<string, object>();
		object Describe(Type t)
		{
			if (Nullable.GetUnderlyingType(t) is { } underlying) return new { anyOf = new[] { Describe(underlying), new { type = "null" } } };
			if (t == typeof(object)) return new { };
			if (t == typeof(string) || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(Guid)) return new { type = new[] { "string", "null" } };
			if (t == typeof(bool)) return new { type = "boolean" };
			if (t.IsEnum) return new { type = new[] { "string", "integer" } };
			if (t.IsPrimitive || t == typeof(decimal)) return new { type = t == typeof(float) || t == typeof(double) || t == typeof(decimal) ? "number" : "integer" };
			if (t.GetInterfaces().Append(t).Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)))) return new { type = new[] { "object", "null" }, additionalProperties = true };
			var sequence = t.GetInterfaces().Append(t).FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
			if (sequence is not null) return new { type = new[] { "array", "null" }, items = Describe(sequence.GetGenericArguments()[0]) };
			if (t.IsInterface || t.Namespace?.StartsWith("System", StringComparison.Ordinal) == true) return new { };
			var key = TypeKey(t);
			if (!definitions.ContainsKey(key))
			{
				definitions[key] = new { };
				definitions[key] = new { type = new[] { "object", "null" }, properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(p => p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always)
					.ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), p => Describe(p.PropertyType)) };
			}
			return new Dictionary<string, object> { ["$ref"] = "#/$defs/" + key };
		}
		var result = Describe(type);
		return new Dictionary<string, object> { ["$schema"] = "https://json-schema.org/draft/2020-12/schema", ["allOf"] = new[] { result }, ["$defs"] = definitions };
	}
}
