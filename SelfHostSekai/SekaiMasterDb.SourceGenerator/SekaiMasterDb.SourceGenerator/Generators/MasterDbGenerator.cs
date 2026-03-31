using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using System.Linq;
using System.IO;
using System.Collections.Generic;

namespace SekaiMasterDb.SourceGenerator
{
    [Generator]
    public class MasterDbGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var jsonFiles = context.AdditionalFiles
                .Where(f => f.Path.EndsWith(".json"))
                .Select(f => Path.GetFileName(f.Path))
                .ToList();

            if (jsonFiles.Count == 0) return;

            // Find all types in Compilation that start with "Master"
            var masterTypes = new Dictionary<string, string>();
            var stack = new Stack<INamespaceSymbol>();
            stack.Push(context.Compilation.GlobalNamespace);
            while (stack.Count > 0)
            {
                var ns = stack.Pop();
                foreach (var member in ns.GetMembers())
                {
                    if (member is INamespaceSymbol childNs)
                    {
                        stack.Push(childNs);
                    }
                    else if (member is INamedTypeSymbol typeSymbol)
                    {
                        if (typeSymbol.Name.StartsWith("Master") && typeSymbol.DeclaredAccessibility == Accessibility.Public)
                        {
                            masterTypes[typeSymbol.Name] = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                        }
                    }
                }
            }

            var mappedPairs = new List<(string json, string modelFqn, string prop)>();

            foreach (var jf in jsonFiles)
            {
                var modelFqn = ToModelName(jf, masterTypes);
                if (modelFqn != null)
                {
                    var propName = jf.Substring(0, jf.Length - 5);
                    propName = char.ToUpperInvariant(propName[0]) + propName.Substring(1);

                    if (propName == "Event") propName = "@Event";

                    mappedPairs.Add((jf, modelFqn, propName));
                }
            }

            if (mappedPairs.Count == 0) return;

            // Sort so the output is deterministic
            mappedPairs.Sort((a, b) => a.prop.CompareTo(b.prop));

            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.IO;");
            sb.AppendLine("using SekaiApiModel.Sekai;");
            sb.AppendLine();
            sb.AppendLine("namespace SekaiMasterDb");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class MasterDb");
            sb.AppendLine("    {");
            sb.AppendLine("        public string DbPath { get; }");
            sb.AppendLine();
            
            foreach (var pair in mappedPairs)
            {
                var cleanProp = pair.prop.StartsWith("@") ? pair.prop : pair.prop;
                sb.AppendLine($"        public Lazy<MasterData<{pair.modelFqn}>> {cleanProp} {{ get; }}");
            }

            sb.AppendLine();
            sb.AppendLine("        public MasterDb(string dbPath = \"./MasterDb\")");
            sb.AppendLine("        {");
            sb.AppendLine("            DbPath = dbPath;");
            
            foreach (var pair in mappedPairs)
            {
                var cleanProp = pair.prop.StartsWith("@") ? pair.prop : pair.prop;
                sb.AppendLine($"            {cleanProp} = new Lazy<MasterData<{pair.modelFqn}>>(() => new MasterData<{pair.modelFqn}>(Path.Combine(DbPath, \"{pair.json}\")));");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            context.AddSource("MasterDb.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private string ToModelName(string jsonFile, Dictionary<string, string> models)
        {
            var baseName = jsonFile.Substring(0, jsonFile.Length - 5);
            baseName = char.ToUpperInvariant(baseName[0]) + baseName.Substring(1);

            if (baseName.EndsWith("ies"))
            {
                var s = "Master" + baseName.Substring(0, baseName.Length - 3) + "y";
                if (models.TryGetValue(s, out var fqn)) return fqn;
            }
            if (baseName.EndsWith("ses"))
            {
                var s = "Master" + baseName.Substring(0, baseName.Length - 2);
                if (models.TryGetValue(s, out var fqn)) return fqn;
            }
            if (baseName.EndsWith("s") && !baseName.EndsWith("ss"))
            {
                var s = "Master" + baseName.Substring(0, baseName.Length - 1);
                if (models.TryGetValue(s, out var fqn)) return fqn;
            }

            var fallback = "Master" + baseName;
            if (models.TryGetValue(fallback, out var fqnFallback)) return fqnFallback;

            return null;
        }
    }
}
