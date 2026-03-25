参考

https://bbs.kanxue.com/thread-287964.htm

apktool 开！ida 开！frida 开！所有系统启动启动启动！

于是我们就获得了 `libil2cpp.so` 跟 `global-metadata.dat`

## global-metadata.dat 毫无悬念地被加密了

直接看看apk里的 `global-metadata.dat`，header是 `56bef089`，被加密了

可以用 [zygisk-il2cppdumper](https://github.com/Perfare/Zygisk-Il2CppDumper) 直接生成dump.cs

我这边选择了跟文章走，字符串常量表搜 `global-metadata.dat`，找到了  `vm::MetadataLoader::LoadMetadataFile`，把地址填到脚本里就好了~  
然后你还会发现虽然提取出来了，但是游戏崩掉了，因为有人在检测dlopen这个函数有没有被hook  
脚本扔在 `DumpMetadata.js` 里了

把他跟着 `libil2cpp.so` 丢到il2cppdumper里，有个警告提示存在 `init_proc`，但他不知道保护了个啥，总之il2cppdumper结果是正确的

## Sekai API

ida-pro-mcp启动，dnspy-mcp启动（顺便增强一下对il2cpp的支持）

接下来是AI的总结

### 1. 发起请求与序列化 (Request & Serialization)
所有的业务层接口调用都由统一的框架类管理，入口通常在这里：
* **业务调用**：游戏内某个功能通过调用 `Sekai.APIManager` (例如 `.CallAPI<A, B>()`) 或 `Sekai.APIExecutor` 传入具体的请求泛型类（例如 `A = LoginRequest`）。
* **序列化**：随后逻辑传递给底层 `Sekai.APICore<A,B>`。在发送之前，客户端并**不会传递明文 JSON**，而是使用 **MessagePack (MsgPack)** 这一套二进制序列化框架将业务请求体转换成极其紧凑的纯二进制数组 `byte[]`。

### 2. Payload 加密层 (AES Cryptography)
为了防止数据被简单抓包和随意篡改，通过序列化的明文字节流会进入专属的安全模块处理：
* `Sekai.APIManager` 会调取其属下的 `Crypt` 对象组件。其内部底层实际调用的是 `CP.FastAESCrypt::Encrypt(byte[])`。
* **加密方式**：这个包装类底层依赖 C# 的 `System.Security.Cryptography.AesManaged`，并在初始化时设定死使用 **AES-256-CBC 算法**、**PKCS7 填充**整个应用的 Key 和 IV 可以认为是同频的。
* **生成载荷**：MsgPack 二进制流被上述环境数据加密后，产生出完全不可读的最终密文。

### 3. 网络传输与投递 (Transport Layer)
构建完成后的最终二进制体，会被交给 `CP.UnityWebRequestClient` 以发起真实的网络请求：
* 无论是 **HTTP POST** 还是 **HTTP PUT**，客户端会调用例如 `CP.UnityWebRequestClient.Post / Put`。
* 内部生成 Unity 游戏引擎原生的 `UnityEngine.Networking.UnityWebRequest`。并将那些经过加密的 byte[] 作为 Raw 数据直接塞入 `UploadHandlerRaw` 中。
* 设置对应的元数据 Headers，比如：加上 `application/octet-stream`（声明自己是单纯的字节流而不是 JSON）、带上我们之前看到的 `SignedCookie` 认证缓存、塞入设备的 UserAgent 等。
* 最终利用原生协程挂起或者 `SendWebRequest()` 将请求发出去。

### 4. 服务端接收响应与解密提取 (Response & Decrypt)
服务器响应成功，返回客户端的仍然是一段毫无表征意义的 `application/octet-stream` 流（且被同样的 AES-CBC 模式加密过）。客户端流程进行逆向处理：
* `CP.UnityWebRequestClient` 分析 HTTP Code 返回为 200，随后提取 `DownloadHandler` 收到的 raw `[byte[]]` 交付回父级协程处理。
* 数据推入泛型层的反向解析器 —— `Sekai.APICore.ConvertResponse<B>()` 方法。
* **解密执行**：再次调用 `CP.FastAESCrypt.DecryptBytes()` 或者其底层的 AES Provider，拿着同一份 Key 和 IV 把密文解密回明文的二进制字节流。

### 5. 反序列化与业务回调 (Deserialization & Callback)
* **反序列化还原**：明文字节流被塞进 MsgPack 解析器，像 Python 脚本的 `msgpack.unpackb` 类似，将它实例化为 C# 内部的 `B` 类型对象（如返回 `LoginResponse` 实例）。
* **UI 回调**：APICore 抛出 `CallBackReponse()` 和 `OnAPICompleted` 相关的 Event/Delegate 委托事件，游戏 UI 接收到该模型之后刷新角色的资产、更新体力或者进入下一个界面。

---

**核心流程简图：**
`[业务明文 Object]` 👉 `MsgPack 序列化` 👉 `AES-CBC 加密` 👉 `[UnityWebRequest / octet-stream 传输]` ☁️ `[游戏服务器]` ☁️ `[UnityWebRequest 接收]` 👉 `AES-CBC 解密` 👉 `MsgPack 反序列化` 👉 `[业务响应 Object]`

### 顺便让他生成一个导出Key的东西~

放在 `DumpNetworkKey.js` 里了  
细心的你一定会发现api有一些名称对不上，比如
```js
domain_get_assemblies: new NativeFunction(getExport("il2cpp_class_from_name"), 'pointer', ['pointer', 'pointer']),
```

但是如果你把他对上了的话，你会发现，反而跑不了了  
看看他导出的`il2cpp_domain_get_assemblies`

```C
__int64 __fastcall il2cpp_domain_get_assemblies(__int64 a1)
{
  return a1 + 32;
}
```

而正常的应该是这样的

```C
__int64 __fastcall il2cpp_domain_get_assemblies(__int64 a1, __int64 *a2)
{
  __int64 result; // x0
  __int64 *v4; // t2

  v4 = (__int64 *)sub_40F998();
  result = *v4;
  *a2 = (v4[1] - *v4) >> 3;
  return result;
}
```

而且可以发现，签名和内容都不太对的那个函数，其实应该是这个


```C
__int64 __fastcall il2cpp_class_get_type(__int64 a1)
{
  return a1 + 32;
}
```

导出表被搅乱了，孩子们别怕，特征码会出手，il2cpp的函数特征太明显了，直接搜就完了

或者我们也可以去 `libunity.so` 里去看看他是怎么定位函数的
```C
__int64 __fastcall sub_6B36B0(__int64 a1)
{
  __int64 result; // x0
  void *v2; // x19
  bool v3; // w19
  __int64 v4; // x0

  result = sub_66910C(a1, 0);
  qword_1431CB8 = result;
  if ( result )
  {
    v2 = (void *)sub_669338(result, "il2cpp_init", 0);
    off_1431568 = v2;
    if ( !v2 )
      sub_DD0C54("il2cpp: function il2cpp_init not found\n");
    v3 = v2 != 0;
    qword_1431570 = sub_669338(qword_1431CB8, "il2cpp_init_utf16", 0);
    if ( !qword_1431570 )
    {
      sub_DD0C54("il2cpp: function il2cpp_init_utf16 not found\n");
      v3 = 0;
    }
    ...
```

这就是lookup的方法，看看 `sub_669338`

```C
__int64 __fastcall sub_669338(__int64 a1, __int64 a2, int a3)
{
  v22 = *(_QWORD *)(_ReadStatusReg(TPIDR_EL0) + 40);
  dlerror();
  v6 = sub_1355000(a1, a2);
  v7 = v6;
  if ( !a3 && (!v6 || dlerror()) )
  {
    sub_650108(v19, "Could not load symbol %s : %s\n");
    v14 = 1;
    ...
```

正常情况下，`v6=dlsym(a1, a2)`才对，显然这个函数有问题，跟进去

```C
void *__fastcall sub_14B5EA0(void *a1, const char *a2)
{
  void *(__fastcall *v2)(void *, const char *); // x2

  v2 = *(void *(__fastcall **)(void *, const char *))((char *)&qword_28 + (_QWORD)off_14BBFE0);
  if ( v2 )
    return v2(a1, a2);
  else
    return dlsym(a1, a2);
}
```

哦吼，一个动态的指针，frida插个打印看看
```
[!] 已到达指令 0x14B5EB0
[+] 当前 X2 寄存器值 (绝对地址): 0x7cb6c3992c
[+] X2 落在模块: libhhld-rt.so | RVA: 0x2c92c
```

这个 `liblhhld-rt.so` 又是个什么东西，拖进ida看看，哇全是垃圾代码混淆，gemini说他不想看那我也不看了，还是找特征吧哈哈，或者hook这个sub_14B5EA0也是个不错的选择

dlopen的检测大概率也在这个 `liblhhld-rt.so` 里，有空再看看