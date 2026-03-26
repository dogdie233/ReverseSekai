# C# 脚本工具说明

## 1. GenerateApiModel.cs

这个脚本用于读取指定的 DLL（通常是 Il2CppDumper 导出的 `DummyDll` 里的 `Assembly-CSharp.dll`），从中提取所有包含 `[MessagePackObject]` 特性的类和枚举，并将它们批量转换为 C# 源码文件导出。

### 特性
- 递归解析依赖的所有自定义数据类型并一并导出。
- 将泛型及依赖类的引用变为**完整的绝对命名空间**（`global::...`），解决可能存在的重名/不明确引用的问题。
- 自动检测并关联目标输出目录中的 `.csproj` 所定义的 `RootNamespace`，修复生成类的命名空间。
- 特殊处理 `System` 原生类型以及泛型（例如自动识别并转换 `System.Int32` 为 `int`，`System.Nullable` 为 `?` ）。
- 自动将所有的 `UnityEngine.*` 相关类型映射为 `global::SekaiApiModel.Shared.*`，避免直接引用产生版权侵权及包依赖问题。

### 前置准备

本脚本依赖 **.NET 10** 或更高版本的 SDK 内置脚本运行支持，无需安装额外的第三方全局工具（如 `dotnet-script` 等）。请确保您已安装符合要求的 .NET SDK。

### 使用方法

```bash
dotnet run GenerateApiModel.cs -- <DLL路径> <代码输出目录>
```

**例子：**
```bash
dotnet run ./GenerateApiModel.cs -- ../Reverse/Il2CppDumper/DummyDll/Assembly-CSharp.dll ../SelfHostSekai/SekaiApiModel.Jp
```

> **注意：**
> 请确保 `<DLL路径>` 正确无误，并且最好是在原 Dumper 输出的同级目录下（这样该脚本可以通过 `Mono.Cecil` 的 `DefaultAssemblyResolver` 找到诸如 `mscorlib` 与 `UnityEngine` 等依赖动态库）。
