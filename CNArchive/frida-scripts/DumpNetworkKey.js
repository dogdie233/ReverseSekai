// agent.js
let isDumped = false;

function startHook() {
    const il2cpp = Module.getBaseAddress("libil2cpp.so");
    if (!il2cpp) {
        console.log("等待 libil2cpp.so 加载...");
        setTimeout(startHook, 1000);
        return;
    }
    console.log("[*] 发现 libil2cpp.so 基地址: " + il2cpp);

    // ==========================================
    // 1. 绑定 IL2CPP 官方原生 C API
    // ==========================================
    const getExport = (name) => {
        let addr = Module.findExportByName("libil2cpp.so", name);
        if (!addr) throw new Error("找不到导出函数: " + name);
        return addr;
    };

    const api = {  // For CN
        domain_get: new NativeFunction(getExport("il2cpp_domain_get"), 'pointer', []),
        thread_attach: new NativeFunction(getExport("il2cpp_thread_attach"), 'pointer', ['pointer']),
        domain_get_assemblies: new NativeFunction(getExport("il2cpp_class_from_name"), 'pointer', ['pointer', 'pointer']),
        class_from_name: new NativeFunction(getExport("il2cpp_image_get_name"), 'pointer', ['pointer', 'pointer', 'pointer']),
        class_get_method_from_name: new NativeFunction(getExport("il2cpp_class_get_type"), 'pointer', ['pointer', 'pointer', 'int']),
        class_get_field_from_name: new NativeFunction(getExport("il2cpp_class_get_field_from_name"), 'pointer', ['pointer', 'pointer']),
        field_get_offset: new NativeFunction(getExport("il2cpp_field_get_offset"), 'int', ['pointer']),
        runtime_invoke: new NativeFunction(getExport("il2cpp_runtime_invoke"), 'pointer', ['pointer', 'pointer', 'pointer', 'pointer']),
        object_unbox: new NativeFunction(getExport("il2cpp_object_unbox"), 'pointer', ['pointer'])
    };

    // 绑定线程以允许调用 invoke
    const domain = api.domain_get();
    api.thread_attach(domain);

    // ==========================================
    // 2. 封装辅助查找函数：全自动搜寻 Class
    // ==========================================
    function findClass(name_space, class_name) {
        let sizePtr = Memory.alloc(Process.pointerSize);
        let assemblies = api.domain_get_assemblies(domain, sizePtr);
        // 读取 size_t (保证 32/64 位兼容)
        let count = Process.pointerSize === 8 ? sizePtr.readU64().toNumber() : sizePtr.readU32();

        let nsPtr = Memory.allocUtf8String(name_space);
        let clsPtr = Memory.allocUtf8String(class_name);

        for (let i = 0; i < count; i++) {
            let assembly = assemblies.add(i * Process.pointerSize).readPointer();
            let image = assembly.readPointer();
            let klass = api.class_from_name(image, nsPtr, clsPtr);
            if (!klass.isNull()) {
                return klass;
            }
        }
        return NULL;
    }

    // 辅助函数：将 C# 的 byte[] 转换为 Hex 字符串
    function readIl2CppByteArray(arrPtr) {
        if (arrPtr.isNull()) return "null";
        // IL2CPP 数组结构内存布局：
        // 64位：0x18 处是长度，0x20 开始是数据
        // 32位：0x0C 处是长度，0x10 开始是数据
        let is64 = Process.pointerSize === 8;
        let lengthOffset = is64 ? 0x18 : 0x0C;
        let elementsOffset = is64 ? 0x20 : 0x10;

        let len = arrPtr.add(lengthOffset).readU32();
        let buffer = arrPtr.add(elementsOffset).readByteArray(len);
        let view = new Uint8Array(buffer);
        let hex = [];
        for (let i = 0; i < view.length; i++) {
            hex.push(view[i].toString(16).padStart(2, '0'));
        }
        return hex.join('');
    }

    // ==========================================
    // 3. 核心提取逻辑
    // ==========================================
    function extractAES(fastAESCryptObj) {
        if (isDumped || fastAESCryptObj.isNull()) return;

        console.log("\n[+] 拦截到 APIManager.Crypt，开始提取...");

        try {
            // 定位 FastAESCrypt 类与字段 aesAlgo
            let FastAESCryptCls = findClass("CP", "FastAESCrypt");
            let aesAlgoField = api.class_get_field_from_name(FastAESCryptCls, Memory.allocUtf8String("aesAlgo"));
            let offset = api.field_get_offset(aesAlgoField);
            
            // 读取 aesAlgo 对象指针
            let aesAlgoObj = fastAESCryptObj.add(offset).readPointer();
            if (aesAlgoObj.isNull()) {
                console.log("[-] aesAlgo 尚未初始化！");
                return;
            }

            console.log("[+] 成功拿到 aesAlgo 指针: " + aesAlgoObj);

            // 定位 AesManaged 类
            let AesManagedCls = findClass("System.Security.Cryptography", "AesManaged");
            
            // 获取各个属性的 get 方法
            let getKeyMethod = api.class_get_method_from_name(AesManagedCls, Memory.allocUtf8String("get_Key"), 0);
            let getIVMethod = api.class_get_method_from_name(AesManagedCls, Memory.allocUtf8String("get_IV"), 0);
            let getModeMethod = api.class_get_method_from_name(AesManagedCls, Memory.allocUtf8String("get_Mode"), 0);
            let getPadMethod = api.class_get_method_from_name(AesManagedCls, Memory.allocUtf8String("get_Padding"), 0);

            let exc = Memory.alloc(Process.pointerSize);

            // Runtime Invoke 触发 get 方法，获取结果
            let keyArrObj = api.runtime_invoke(getKeyMethod, aesAlgoObj, NULL, exc);
            let ivArrObj = api.runtime_invoke(getIVMethod, aesAlgoObj, NULL, exc);
            let modeObj = api.runtime_invoke(getModeMethod, aesAlgoObj, NULL, exc);
            let padObj = api.runtime_invoke(getPadMethod, aesAlgoObj, NULL, exc);

            // 解析 C# Array 为 Hex
            let keyHex = readIl2CppByteArray(keyArrObj);
            let ivHex = readIl2CppByteArray(ivArrObj);

            // C# 的 Enum 返回的是装箱(Boxed)对象，需要 Unbox 取出真实的 int 值
            let modeValue = api.object_unbox(modeObj).readInt();
            let padValue = api.object_unbox(padObj).readInt();

            console.log("=========================================");
            console.log("🔑 AES Key     : " + keyHex);
            console.log("🛡️ AES IV      : " + ivHex);
            console.log("⚙️ Cipher Mode : " + modeValue + " (1=CBC, 2=ECB)");
            console.log("📦 Padding     : " + padValue + " (2=PKCS7)");
            console.log("=========================================\n");

            isDumped = true;
        } catch (e) {
            console.error("[-] 解析发生异常: " + e);
        }
    }

    // ==========================================
    // 4. 定位与 Hook 注入点
    // ==========================================
    console.log("[*] 正在扫描类 Sekai.APIManager...");
    let APIManagerCls = findClass("Sekai", "APIManager");
    if (APIManagerCls.isNull()) {
        console.error("[-] 找不到 APIManager 类");
        return;
    }

    let getCryptMethod = api.class_get_method_from_name(APIManagerCls, Memory.allocUtf8String("get_Crypt"), 0);
    if (getCryptMethod.isNull()) {
        console.error("[-] 找不到 get_Crypt 方法");
        return;
    }

    // MethodInfo 结构体的第一个字段就是实际指令所在的内存地址 (methodPointer)
    let getCryptAddr = getCryptMethod.readPointer();
    console.log("[*] 成功解析 APIManager.get_Crypt() 地址: " + getCryptAddr);

    Interceptor.attach(getCryptAddr, {
        onLeave: function (retval) {
            if (!isDumped && !retval.isNull()) {
                extractAES(retval);
            }
        }
    });
    
    console.log("[*] Hook 启动就绪，请在游戏中触发网络请求...");
}

setImmediate(startHook);