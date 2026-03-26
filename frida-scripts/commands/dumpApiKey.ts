import "frida-il2cpp-bridge";

// https://mos9527.com/posts/pjsk/archive-20240105
export default function dumpApiKey() {
    Il2Cpp.perform(() => {
        const game = Il2Cpp.domain.assembly("Assembly-CSharp").image;
        const apiManager = game.class("Sekai.APIManager");
        const instance = apiManager.method<Il2Cpp.Object>("get_Instance").invoke();
        const crypt = instance.method<Il2Cpp.Object>("get_Crypt").invoke();
        const aes = crypt.field<Il2Cpp.Object>("aesAlgo");
        
        const key = aes.value.method("get_Key").invoke();
        const iv = aes.value.method("get_IV").invoke();
        const mode = aes.value.method("get_Mode").invoke();
        const padding = aes.value.method("get_Padding").invoke();

        // byte数组，需要转换成16进制字符串
        const keyHex = Array.from(key).map((b) => b.toString(16).padStart(2, "0")).join("");
        const ivHex = Array.from(iv).map((b) => b.toString(16).padStart(2, "0")).join("");
        
        console.log("Found API Key:");
        console.log("=========================================");
        console.log("🔑 AES Key     : " + keyHex);
        console.log("🛡️ AES IV      : " + ivHex);
        console.log("⚙️ Cipher Mode : " + mode);
        console.log("📦 Padding     : " + padding);
        console.log("=========================================\n");
    });
}