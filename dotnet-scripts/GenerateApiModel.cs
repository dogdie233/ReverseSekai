#!/usr/bin/env dotnet run
#:package Mono.Cecil@0.11.5
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;
using Mono.Cecil;

if (args.Length < 2)
{
    Console.WriteLine("Usage: dotnet run GenerateApiModel.cs -- <dll路径> <输出目录>");
    return;
}

var dllPath = args[0];
var outputDir = args[1];

if (!File.Exists(dllPath))
{
    Console.WriteLine($"DLL not found: {dllPath}");
    return;
}

// 找到最近的 csproj 和 RootNamespace
string FindRootNamespace(string dir)
{
    var currentDir = new DirectoryInfo(dir);
    while (currentDir != null)
    {
        var csprojFiles = currentDir.GetFiles("*.csproj");
        if (csprojFiles.Length > 0)
        {
            try
            {
                var doc = XDocument.Load(csprojFiles[0].FullName);
                var rootNamespaceNode = doc.Descendants("RootNamespace").FirstOrDefault();
                if (rootNamespaceNode != null)
                {
                    return rootNamespaceNode.Value;
                }
            }
            catch { }
            
            // 如果只有 csproj 没有 RootNamespace，通常默认等于文件名去后缀
            return Path.GetFileNameWithoutExtension(csprojFiles[0].Name);
        }
        currentDir = currentDir.Parent;
    }
    return null;
}

var rootNamespace = FindRootNamespace(outputDir);

Console.WriteLine($"Discovered RootNamespace: {rootNamespace}");

var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(dllPath));
var parameters = new ReaderParameters { AssemblyResolver = resolver };
var assembly = AssemblyDefinition.ReadAssembly(dllPath, parameters);

bool HasMessagePackObject(TypeDefinition type)
{
    return type.CustomAttributes.Any(a => a.AttributeType.Name == "MessagePackObjectAttribute");
}

var typesToDump = new HashSet<TypeDefinition>(assembly.MainModule.Types.Where(t => HasMessagePackObject(t) && !t.IsEnum));

var typesToProcess = new Queue<TypeDefinition>(typesToDump);

void CollectTypesToDump(TypeReference typeRef)
{
    if (typeRef == null) return;
    
    if (typeRef.IsArray)
    {
        CollectTypesToDump(typeRef.GetElementType());
        return;
    }
    
    if (typeRef.IsGenericInstance)
    {
        var git = (GenericInstanceType)typeRef;
        foreach(var arg in git.GenericArguments)
        {
            CollectTypesToDump(arg);
        }
        return;
    }
    
    var resolved = typeRef.Resolve();
    if (resolved != null && resolved.Module == assembly.MainModule && !typesToDump.Contains(resolved))
    {
        typesToDump.Add(resolved);
        typesToProcess.Enqueue(resolved);
    }
}

while(typesToProcess.Count > 0)
{
    var type = typesToProcess.Dequeue();
    foreach(var field in type.Fields)
    {
        if (field.CustomAttributes.Any(a => a.AttributeType.Name == "KeyAttribute"))
        {
            CollectTypesToDump(field.FieldType);
        }
    }
}


// 提前构建类全名到最新命名空间的映射
var typeToNamespace = new Dictionary<string, string>();
foreach (var type in typesToDump)
{
    var ns = type.Namespace;
    if (string.IsNullOrEmpty(ns)) ns = "Global";
    var finalNs = !string.IsNullOrEmpty(rootNamespace) ? $"{rootNamespace}.{ns}" : ns;
    typeToNamespace[type.FullName] = finalNs;
}

string GetTypeName(TypeReference typeRef)
{
    var typeMapping = new Dictionary<string, string>
    {
        { "System.Int32", "int" },
        { "System.String", "string" },
        { "System.Boolean", "bool" },
        { "System.Int64", "long" },
        { "System.Byte", "byte" },
        { "System.Single", "float" },
        { "System.Double", "double" },
        { "System.Decimal", "decimal" },
        { "System.Int16", "short" },
        { "System.UInt32", "uint" },
        { "System.UInt64", "ulong" },
        { "System.UInt16", "ushort" },
        { "System.SByte", "sbyte" },
        { "System.Char", "char" },
        { "System.Object", "object" },
        { "System.Void", "void" }
    };

    if (typeRef.IsArray)
        return GetTypeName(typeRef.GetElementType()) + "[]";
    if (typeRef.IsGenericInstance)
    {
        var git = (GenericInstanceType)typeRef;
        var name = git.ElementType.Name.Split('`')[0];
        
        if (git.ElementType.FullName == "System.Nullable`1")
        {
            return $"{GetTypeName(git.GenericArguments[0])}?";
        }

        var args = string.Join(", ", git.GenericArguments.Select(GetTypeName));
        
        if (typeMapping.TryGetValue(git.ElementType.FullName, out string mappedName))
        {
            return $"{mappedName}<{args}>"; // rare
        }
        
        if (git.ElementType.Namespace != null && git.ElementType.Namespace.StartsWith("UnityEngine"))
        {
            return $"global::SekaiApiModel.Shared.{name}<{args}>";  // Map UnityEngine Generics
        }

        if (git.ElementType.Namespace == "System.Collections.Generic" || git.ElementType.Namespace == "System.Collections")
        {
            return $"global::{git.ElementType.Namespace}.{name}<{args}>";
        }
        
        if (typeToNamespace.TryGetValue(git.ElementType.FullName, out string genericNs))
        {
            return $"global::{genericNs}.{name}<{args}>"; // type mappings we dumped
        }

        var nsFallbackG = git.ElementType.Namespace;
        if (string.IsNullOrEmpty(nsFallbackG)) 
        {
            return $"{name}<{args}>"; // global empty namespace
        }
        return $"global::{nsFallbackG}.{name}<{args}>"; 
    }
    
    if (typeMapping.TryGetValue(typeRef.FullName, out string mapped))
    {
        return mapped;
    }

    if (typeRef.Namespace != null && typeRef.Namespace.StartsWith("UnityEngine"))
    {
        return $"global::SekaiApiModel.Shared.{typeRef.Name.Replace("/", ".")}";
    }
    
    if (typeToNamespace.TryGetValue(typeRef.FullName, out string mappedNs))
    {
        var cleanName = typeRef.Name.Replace("/", ".");
        return $"global::{mappedNs}.{cleanName}";
    }
    
    var nsFallback = typeRef.Namespace;
    if (string.IsNullOrEmpty(nsFallback)) 
    {
        return typeRef.Name.Replace("/", ".");
    }
    return $"global::{nsFallback}.{typeRef.Name.Replace("/", ".")}";
}

foreach (var type in typesToDump)
{
    var ns = type.Namespace;
    if (string.IsNullOrEmpty(ns)) ns = "Global";
    
    var dir = Path.Combine(outputDir, ns.Replace(".", Path.DirectorySeparatorChar.ToString()));
    Directory.CreateDirectory(dir);
    
    var finalNs = ns;
    if (!string.IsNullOrEmpty(rootNamespace))
    {
        finalNs = $"{rootNamespace}.{ns}";
    }

    var filePath = Path.Combine(dir, type.Name + ".cs");
    using var writer = new StreamWriter(filePath);
    
    var usings = new HashSet<string> { "System", "MessagePack", "System.Collections.Generic" };

    writer.WriteLine("using System;");
    writer.WriteLine("using MessagePack;");
    writer.WriteLine("using System.Collections.Generic;");

    writer.WriteLine();
    writer.WriteLine($"namespace {finalNs}");
    writer.WriteLine("{");
    
    if (type.IsEnum)
    {
        writer.WriteLine($"    public enum {type.Name}");
        writer.WriteLine("    {");
        foreach(var field in type.Fields.Where(f => f.Name != "value__"))
        {
            writer.WriteLine($"        {field.Name} = {field.Constant},");
        }
        writer.WriteLine("    }");
    }
    else
    {
        if (HasMessagePackObject(type))
        {
            var isKeyString = type.CustomAttributes.First(a => a.AttributeType.Name == "MessagePackObjectAttribute")
                                  .ConstructorArguments.FirstOrDefault().Value?.ToString() == "True";
            writer.WriteLine($"    [MessagePackObject({(isKeyString ? "true" : "")})]");
        }
    
        writer.WriteLine($"    public class {type.Name}");
        writer.WriteLine("    {");
        
        foreach (var field in type.Fields)
        {
            var keyAttr = field.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "KeyAttribute");
            if (keyAttr != null)
            {
                var keyVal = keyAttr.ConstructorArguments.FirstOrDefault().Value;
                var keyStr = keyVal is string ? $"\"{keyVal}\"" : keyVal?.ToString();
                writer.WriteLine($"        [Key({keyStr})]");
                writer.WriteLine($"        public {GetTypeName(field.FieldType)} {field.Name};");
                writer.WriteLine();
            }
        }
        
        writer.WriteLine("    }");
    }
    writer.WriteLine("}");
    Console.WriteLine($"Generated {filePath}");
}

