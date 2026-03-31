import "frida-il2cpp-bridge";

import dumpApiKey from "./commands/dumpApiKey";

Il2Cpp.perform(() => {
    const UnityEngine = Il2Cpp.Domain.assembly("UnityEngine.CoreModule").image;
    const Debug = UnityEngine.class("UnityEngine.Debug");

    // Hook Debug.Log(object message)
    Debug.method("Log", 1).overload("System.Object").implementation = function (message) {
        console.log(`[C# Debug.Log] ${message.toString()}`);
        return this.method("Log").invoke(message);
    };

    // 如果你想 Hook 错误日志
    Debug.method("LogError", 1).overload("System.Object").implementation = function (message) {
        console.log(`[C# Debug.LogError] ${message.toString()}`);
        return this.method("LogError").invoke(message);
    };
});

global.dumpApiKey = dumpApiKey;